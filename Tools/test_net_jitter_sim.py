#!/usr/bin/env python3
"""Regression tests for the HECTON-8 lockstep jitter simulator."""

from __future__ import annotations

import argparse
import os
import shutil
import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import NetJitterSim as net_sim


def build_args(
    *,
    ticks: int = 180,
    clients: int = 2,
    input_delay_ticks: int = 16,
    redundancy: int = 16,
    loss_percent: str = "5",
) -> argparse.Namespace:
    return argparse.Namespace(
        latency_ms=200,
        jitter_ms=40,
        loss_bps=net_sim.parse_percent_bps(loss_percent),
        tick_ms=20,
        ticks=ticks,
        clients=clients,
        input_delay_ticks=input_delay_ticks,
        rollback_ticks=64,
        redundancy=redundancy,
        seed=0x4E455431,
        report=None,
    )


class NetJitterSimTests(unittest.TestCase):
    def assert_ready(self, report: dict) -> None:
        self.assertEqual("NETWORK PROTOCOL READY", report["status"])
        verification = report["verification"]
        self.assertEqual(0, verification["master_state_hash_mismatches"])
        self.assertEqual(0, verification["input_ring_mismatches"])
        self.assertEqual(0, verification["missing_actual_inputs"])
        self.assertEqual("PASS", verification["float_hash_audit"]["status"])

    def test_baseline_latency_loss_converges(self) -> None:
        report = net_sim.simulate(build_args())

        self.assert_ready(report)
        self.assertEqual(200, report["config"]["latency_ms"])
        self.assertEqual("5%", report["config"]["loss_percent_text"])
        self.assertGreater(report["network"]["lost_packets"], 0)
        self.assertEqual(0, report["rollback"]["events"])

    def test_rollback_stress_corrects_predicted_inputs(self) -> None:
        report = net_sim.simulate(build_args(input_delay_ticks=8))

        self.assert_ready(report)
        self.assertGreater(report["rollback"]["events"], 0)
        self.assertGreater(report["rollback"]["predicted_slots"], 0)
        self.assertGreater(report["rollback"]["corrected_slots"], 0)
        self.assertLessEqual(report["rollback"]["max_depth_ticks"], 64)
        self.assertEqual(0, report["rollback"]["too_old_corrections"])

    def test_four_client_fanout_converges(self) -> None:
        report = net_sim.simulate(build_args(clients=4))

        self.assert_ready(report)
        self.assertEqual(4, report["config"]["clients"])
        self.assertGreater(report["network"]["sent_packets"], report["network"]["delivered_packets"])
        self.assertGreater(report["network"]["estimated_payload_bytes_per_second"], 0)

    def test_redundant_packet_records_clamp_to_available_ticks(self) -> None:
        early_records = net_sim.packet_records_for_tick(sender=1, wall_tick=5, max_tick=100, redundancy=16)
        late_records = net_sim.packet_records_for_tick(sender=1, wall_tick=200, max_tick=100, redundancy=16)

        self.assertEqual(6, len(early_records))
        self.assertEqual(0, early_records[0].tick)
        self.assertEqual(5, early_records[-1].tick)
        self.assertEqual(16, len(late_records))
        self.assertEqual(84, late_records[0].tick)
        self.assertEqual(99, late_records[-1].tick)

    def test_packet_schema_offsets_sizes_and_mtu_budget_are_locked(self) -> None:
        self.assertEqual([], net_sim.validate_packet_schema())
        specs = net_sim.schema_by_name()

        envelope = specs["H8NetEnvelope"]
        input_record = specs["InputStateRecord"]
        world_header = specs["WorldDeltaHeader"]
        world_record = specs["WorldDeltaRecord"]

        self.assertEqual(24, envelope.size)
        self.assertEqual(16, input_record.size)
        self.assertEqual(32, world_header.size)
        self.assertEqual(32, world_record.size)
        self.assertEqual(0, envelope.fields[0].offset)
        self.assertEqual("Magic", envelope.fields[0].name)
        self.assertEqual(20, envelope.fields[-1].offset)
        self.assertEqual("AckMask32", envelope.fields[-1].name)
        self.assertLessEqual(envelope.size + (16 * input_record.size), net_sim.NET_MTU_PAYLOAD_BYTES)
        self.assertLessEqual(envelope.size + world_header.size + world_record.size, net_sim.NET_MTU_PAYLOAD_BYTES)

    def test_aup64_round_trips_boundaries_and_flags_overflow(self) -> None:
        cases = (
            (0, 0, 0),
            (net_sim.AUP_AXIS_MIN, net_sim.AUP_AXIS_MAX, -1),
            (net_sim.AUP_AXIS_MAX, net_sim.AUP_AXIS_MIN, 1),
            (512000, -512000, 123456),
        )

        for x_mm, y_mm, z_mm in cases:
            with self.subTest(case=(x_mm, y_mm, z_mm)):
                packed = net_sim.pack_aup_local64(x_mm, y_mm, z_mm)
                unpacked_x, unpacked_y, unpacked_z, overflow = net_sim.unpack_aup_local64(packed)

                self.assertFalse(overflow)
                self.assertEqual((x_mm, y_mm, z_mm), (unpacked_x, unpacked_y, unpacked_z))

        overflow_cases = (
            (net_sim.AUP_AXIS_MAX + 1, 0, 0),
            (0, net_sim.AUP_AXIS_MIN - 1, 0),
            (0, 0, net_sim.AUP_AXIS_MAX + 1),
        )
        for x_mm, y_mm, z_mm in overflow_cases:
            with self.subTest(overflow_case=(x_mm, y_mm, z_mm)):
                packed = net_sim.pack_aup_local64(x_mm, y_mm, z_mm)
                self.assertEqual(net_sim.AUP_OVERFLOW_BIT, packed)
                self.assertEqual((0, 0, 0, True), net_sim.unpack_aup_local64(packed))

    def test_merkle_diff_indices_localize_changed_leaves(self) -> None:
        base = tuple(net_sim.hash_state_chunk(index, 10, (index, index * 3)) for index in range(9))
        altered = list(base)
        altered[3] = net_sim.hash_state_chunk(3, 10, (999, 3))
        altered[8] = net_sim.hash_state_chunk(8, 10, (888, 8))

        self.assertEqual([], net_sim.merkle_diff_indices(base, base))
        self.assertEqual([3, 8], net_sim.merkle_diff_indices(base, tuple(altered)))
        with self.assertRaises(ValueError):
            net_sim.merkle_diff_indices(base, base[:-1])

    def test_float_hash_crime_detector_rejects_float_math(self) -> None:
        source = "\n".join(
            (
                "def mix64(hash_value, value):",
                "    return hash_value / 2",
                "",
                "def master_state_hash(tick, state, inputs):",
                "    return 1.0",
                "",
            )
        )

        temp_root = TOOLS_ROOT.parent / ".codex_tmp" / "net_float_crime" / str(os.getpid())
        if temp_root.exists():
            shutil.rmtree(temp_root, ignore_errors=True)
        temp_root.mkdir(parents=True, exist_ok=True)
        try:
            crime_path = temp_root / "crime.py"
            crime_path.write_text(source, encoding="utf-8")
            result = net_sim.audit_hash_functions(crime_path)
        finally:
            shutil.rmtree(temp_root, ignore_errors=True)

        self.assertEqual("CRIME", result["status"])
        self.assertTrue(any("division operator" in entry for entry in result["violations"]))
        self.assertTrue(any("float constant" in entry for entry in result["violations"]))


if __name__ == "__main__":
    unittest.main()
