#!/usr/bin/env python3
"""Tests for the static Unity/process/proof watchdog."""

from __future__ import annotations

import hashlib
import json
import os
import struct
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import unity_process_proof_watchdog as watchdog
import validate_proof_packet as gate


PNG_MAGIC = b"\x89PNG\r\n\x1a\n"


def iso(dt: datetime) -> str:
    return dt.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def write_png_header(path: Path, width: int = 1280, height: int = 720) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = PNG_MAGIC + struct.pack(">I", 13) + b"IHDR" + struct.pack(">II", width, height)
    payload += b"\x08\x02\x00\x00\x00" + b"\x00\x00\x00\x00"
    path.write_bytes(payload)


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class UnityProcessProofWatchdogTests(unittest.TestCase):
    def write_process_sample(self, root: Path, processes: list[dict[str, object]]) -> Path:
        path = root / "processes.json"
        path.write_text(json.dumps(processes), encoding="utf-8")
        return path

    def args(self, root: Path, process_sample: Path, *extra: str):
        return watchdog.build_parser().parse_args(
            [
                "--repo-root",
                str(root),
                "--process-sample-json",
                str(process_sample),
                "--proofgate",
                str(Path(gate.__file__).resolve()),
                "--strict",
                "--max-log-files",
                "4",
                "--no-write",
                *extra,
            ]
        )

    def build_valid_packet(self, root: Path) -> Path:
        packet = root / "Docs" / "Screenshots" / "HectonProofPackets" / "h8_1475_s01"
        screenshots = packet / "screenshots"
        screenshots.mkdir(parents=True)
        base_time = datetime(2026, 6, 4, 18, 0, tzinfo=timezone.utc)
        records: list[dict[str, object]] = []
        for index, view_id, filename in gate.REQUIRED_VIEWS:
            path = screenshots / filename
            write_png_header(path)
            mtime = base_time + timedelta(seconds=index)
            os.utime(path, (mtime.timestamp(), mtime.timestamp()))
            depth = 1.0 if view_id == "underwater_0_5m" else 30.0 if view_id == "underwater_20_50m_route" else 0.0
            records.append(
                {
                    "view_index": index,
                    "view_id": view_id,
                    "production_view": True,
                    "diagnostic_view": False,
                    "file_path": f"screenshots/{filename}",
                    "file_name": filename,
                    "sha256": sha(path),
                    "byte_size": path.stat().st_size,
                    "png_width": 1280,
                    "png_height": 720,
                    "capture_requested_utc": iso(mtime - timedelta(seconds=1)),
                    "file_created_utc": iso(mtime),
                    "file_last_write_utc": iso(mtime),
                    "capture_source": "owned_harness",
                    "camera_name": f"ProofCamera_{view_id}",
                    "camera_position_world": [0.0, 0.0, 0.0],
                    "camera_rotation_euler": [0.0, 0.0, 0.0],
                    "field_of_view_degrees": 60.0,
                    "route_anchor_id": f"anchor_{view_id}",
                    "route_state_id": f"state_{view_id}",
                    "route_state_hash": "0x12345678",
                    "route_predicate_pass": True,
                    "route_predicate_failures": [],
                    "camera_visual_depth_meters": depth,
                    "depth_zone_id": "surface" if depth == 0.0 else "photic",
                    "depth_zone_name": "surface" if depth == 0.0 else "photic",
                    "depth_zone_hash": "0x87654321",
                    "depth_predicate_pass": True,
                    "underwater_active": view_id.startswith("underwater"),
                    "global_quality_weight": 0.6,
                    "global_quality_label": "q060",
                    "render_scale_current": 1.0,
                    "render_scale_target": 1.0,
                    "post_stack_hash": "0xabcdef01",
                    "ui_visible": False,
                    "log_offset_or_timestamp_at_capture": iso(mtime),
                    "packet_id": "h8_1475",
                    "session_id": "s01",
                }
            )
        log = packet / "UnityEditor_h8_1475_s01.log"
        log.write_text("historical Warning\nClean proof window\n", encoding="utf-8")
        log_time = base_time + timedelta(seconds=120)
        os.utime(log, (log_time.timestamp(), log_time.timestamp()))
        prefix = "historical Warning\n"
        manifest = {
            "schema_name": gate.SCHEMA,
            "schema_version": 1,
            "harness_name": "HectonProofHarness",
            "harness_version": "1.0",
            "packet_id": "h8_1475",
            "session_id": "s01",
            "created_utc": iso(base_time + timedelta(seconds=150)),
            "created_local": "2026-06-04T22:02:30+04:00",
            "active_scene": "02_HECTON_WORLD",
            "evidence_class": "UNITY_CAPTURE_PACKET",
            "final_disposition": "ACCEPTED_BY_HARNESS",
            "may_submit_as_runtime_proof": True,
            "global_quality_weight": 0.6,
            "global_quality_label": "q060",
            "route_owner_name": "HectonProofHarness",
            "route_session_id": "s01",
            "camera_source": "owned_harness",
            "ui_policy": "ui_off",
            "log_path": "UnityEditor_h8_1475_s01.log",
            "log_sha256": sha(log),
            "log_window_start_utc": iso(base_time + timedelta(seconds=60)),
            "log_window_end_utc": iso(base_time + timedelta(seconds=121)),
            "log_window_start_offset": len(prefix),
            "log_window_end_offset": len(log.read_text(encoding="utf-8")),
            "post_capture_clean_seconds": 61,
            "derived_checks": {name: True for name in gate.REQUIRED_DERIVED_CHECKS},
            "screenshots": records,
        }
        manifest_path = packet / "manifest.json"
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        manifest_time = base_time + timedelta(seconds=151)
        os.utime(manifest_path, (manifest_time.timestamp(), manifest_time.timestamp()))
        (packet / "manifest.sha256").write_text(sha(manifest_path) + "  manifest.json\n", encoding="utf-8")
        return packet

    def test_busy_unity_process_blocks_slot(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_watchdog_") as tmp:
            root = Path(tmp)
            sample = self.write_process_sample(root, [{"ProcessName": "Unity", "Id": 100, "CPU": 1.0}])
            payload = watchdog.build_status(self.args(root, sample))
            self.assertTrue(payload["unityBusy"])
            self.assertIn("BUSY_DO_NOT_TAKE_SLOT", payload["blockers"])

    def test_raw_mcp_group_blocks_without_manifest(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_watchdog_") as tmp:
            root = Path(tmp)
            sample = self.write_process_sample(root, [])
            mcp = root / "Docs" / "Screenshots" / "MCP"
            for _, view_id, _ in gate.REQUIRED_VIEWS:
                write_png_header(mcp / f"h8_1475_{view_id}.png")
            payload = watchdog.build_status(self.args(root, sample))
            self.assertIn("RAW_PNG_SET_NO_MANIFEST", payload["blockers"])
            self.assertTrue(payload["newestRawGroup"]["hasAllRequiredRouteNames"])

    def test_manifest_packet_runs_proofgate(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_watchdog_") as tmp:
            root = Path(tmp)
            sample = self.write_process_sample(root, [])
            self.build_valid_packet(root)
            payload = watchdog.build_status(self.args(root, sample))
            self.assertEqual(payload["latestProofGateResult"]["status"], gate.PASS_STATUS)
            self.assertFalse(payload["latestProofGateResult"]["mayClaimPlayerCaptureVerified"])
            self.assertFalse(payload["mayClaimPlayerCaptureVerified"])
            self.assertFalse(payload["mayClaimVisualAccepted"])
            self.assertFalse(payload["mayClaimRuntimeProof"])

    def test_dirty_log_token_blocks(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_watchdog_") as tmp:
            root = Path(tmp)
            sample = self.write_process_sample(root, [])
            logs = root / "Docs" / "Logs"
            logs.mkdir(parents=True)
            (logs / "UnityLaunch_test.log").write_text("Asset Pipeline Refresh\n", encoding="utf-8")
            payload = watchdog.build_status(self.args(root, sample))
            self.assertIn("DIRTY_LOG_TOKENS_FOUND", payload["blockers"])
            self.assertIn("DIRTY_LOG_IMPORT", payload["dirtyTokenSummary"]["tokenCountsByCode"])


if __name__ == "__main__":
    unittest.main()
