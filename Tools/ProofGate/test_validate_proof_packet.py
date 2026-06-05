#!/usr/bin/env python3
"""Tests for the HECTON static proof-packet gate."""

from __future__ import annotations

import hashlib
import json
import os
import struct
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

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


class ProofPacketGateTests(unittest.TestCase):
    def build_valid_packet(self, root: Path) -> tuple[Path, dict[str, object]]:
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
        log.write_text("Clean proof window\nH8_CAPTURE_DONE\n", encoding="utf-8")
        log_time = base_time + timedelta(seconds=120)
        os.utime(log, (log_time.timestamp(), log_time.timestamp()))
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
            "post_capture_clean_seconds": 61,
            "derived_checks": {name: True for name in gate.REQUIRED_DERIVED_CHECKS},
            "screenshots": records,
        }
        manifest_path = packet / "manifest.json"
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        manifest_time = base_time + timedelta(seconds=151)
        os.utime(manifest_path, (manifest_time.timestamp(), manifest_time.timestamp()))
        (packet / "manifest.sha256").write_text(sha(manifest_path) + "  manifest.json\n", encoding="utf-8")
        return packet, manifest

    def validate(self, packet: Path, *extra: str) -> tuple[int, dict[str, object]]:
        args = gate.build_parser().parse_args(
            [
                "--packet-root",
                str(packet),
                "--packet-id",
                "h8_1475",
                "--session-id",
                "s01",
                "--expected-quality",
                "q060",
                "--strict",
                *extra,
            ]
        )
        return gate.validate_packet(args)

    def rewrite_manifest(self, packet: Path, manifest: dict[str, object]) -> None:
        manifest_path = packet / "manifest.json"
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        created_utc = gate.parse_timestamp(manifest.get("created_utc"))
        if created_utc is not None:
            timestamp = created_utc.timestamp() + 1.0
            os.utime(manifest_path, (timestamp, timestamp))
        (packet / "manifest.sha256").write_text(sha(manifest_path), encoding="utf-8")

    def test_accepts_valid_packet_static_gate(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, _ = self.build_valid_packet(Path(tmp))
            code, payload = self.validate(packet)
            self.assertEqual(code, 0, payload.get("rejectCodes"))
            self.assertEqual(payload["status"], gate.PASS_STATUS)
            self.assertFalse(payload["mayClaimPlayerCaptureVerified"])
            self.assertEqual(payload["playerCaptureClaimPolicy"], "STATIC_GATE_NEVER_VERIFIES_PLAYER_CAPTURE")

    def test_rejects_manifest_player_capture_claim_boolean(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            manifest["mayClaimPlayerCaptureVerified"] = True
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertFalse(payload["mayClaimPlayerCaptureVerified"])
            self.assertIn("PLAYER_CAPTURE_CLAIM_UNSUPPORTED", payload["rejectCodes"])

    def test_rejects_manifest_player_capture_claim_disposition(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            manifest["final_disposition"] = "PLAYER_CAPTURE_VERIFIED"
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertFalse(payload["mayClaimPlayerCaptureVerified"])
            self.assertIn("PLAYER_CAPTURE_CLAIM_UNSUPPORTED", payload["rejectCodes"])

    def test_rejects_derived_check_player_capture_claim(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            checks = manifest["derived_checks"]
            assert isinstance(checks, dict)
            checks["playerCaptureVerified"] = True
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertFalse(payload["mayClaimPlayerCaptureVerified"])
            self.assertIn("PLAYER_CAPTURE_CLAIM_UNSUPPORTED", payload["rejectCodes"])

    def test_rejects_manifest_player_capture_claim_alias_matrix(self) -> None:
        claim_values = (True, "true", "yes", 1, "PLAYER_CAPTURE_VERIFIED")
        for field_name in gate.PLAYER_CAPTURE_CLAIM_FIELDS:
            for claim_value in claim_values:
                with self.subTest(field_name=field_name, claim_value=claim_value):
                    with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
                        packet, manifest = self.build_valid_packet(Path(tmp))
                        manifest[field_name] = claim_value
                        self.rewrite_manifest(packet, manifest)
                        code, payload = self.validate(packet)
                        self.assertEqual(code, 1)
                        self.assertFalse(payload["mayClaimPlayerCaptureVerified"])
                        self.assertIn("PLAYER_CAPTURE_CLAIM_UNSUPPORTED", payload["rejectCodes"])

    def test_rejects_derived_check_player_capture_claim_alias_matrix(self) -> None:
        claim_values = (True, "true", "yes", 1, "PLAYER_CAPTURE_VERIFIED")
        for field_name in gate.PLAYER_CAPTURE_CLAIM_FIELDS:
            for claim_value in claim_values:
                with self.subTest(field_name=field_name, claim_value=claim_value):
                    with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
                        packet, manifest = self.build_valid_packet(Path(tmp))
                        checks = manifest["derived_checks"]
                        assert isinstance(checks, dict)
                        checks[field_name] = claim_value
                        self.rewrite_manifest(packet, manifest)
                        code, payload = self.validate(packet)
                        self.assertEqual(code, 1)
                        self.assertFalse(payload["mayClaimPlayerCaptureVerified"])
                        self.assertIn("PLAYER_CAPTURE_CLAIM_UNSUPPORTED", payload["rejectCodes"])

    def test_rejects_raw_png_set_without_manifest(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet = Path(tmp) / "packet"
            write_png_header(packet / "screenshots" / "01_surface_coast_aegir_ui_off.png")
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("RAW_PNG_SET", payload["rejectCodes"])

    def test_rejects_png_sha_mismatch(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            manifest["screenshots"][0]["sha256"] = "0" * 64  # type: ignore[index]
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("PNG_SHA256_MISMATCH", payload["rejectCodes"])

    def test_rejects_png_dimension_mismatch(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            manifest["screenshots"][0]["png_width"] = 2048  # type: ignore[index]
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("PNG_DIMENSION_MISMATCH", payload["rejectCodes"])

    def test_rejects_missing_required_view(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            screenshots = manifest["screenshots"]  # type: ignore[assignment]
            assert isinstance(screenshots, list)
            manifest["screenshots"] = [record for record in screenshots if record["view_id"] != "underwater_20_50m_route"]
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("REQUIRED_VIEW_MISSING", payload["rejectCodes"])

    def test_rejects_diagnostic_substitution(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            manifest["screenshots"][1]["production_view"] = False  # type: ignore[index]
            manifest["screenshots"][1]["diagnostic_view"] = True  # type: ignore[index]
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("PRODUCTION_VIEW_MISSING", payload["rejectCodes"])
            self.assertIn("DIAGNOSTIC_SUBSTITUTION", payload["rejectCodes"])

    def test_rejects_route_predicate_false(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            manifest["screenshots"][4]["route_predicate_pass"] = False  # type: ignore[index]
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("ROUTE_PREDICATE_FAIL", payload["rejectCodes"])

    def test_rejects_underwater_depth_outside_band(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            manifest["screenshots"][2]["camera_visual_depth_meters"] = 0.05  # type: ignore[index]
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("DEPTH_PREDICATE_FAIL", payload["rejectCodes"])

    def test_rejects_dirty_log_token(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            log = packet / "UnityEditor_h8_1475_s01.log"
            log.write_text("Asset Pipeline Refresh\n", encoding="utf-8")
            manifest["log_sha256"] = sha(log)
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("DIRTY_LOG_IMPORT", payload["rejectCodes"])

    def test_scans_declared_log_window_offsets(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            log = packet / "UnityEditor_h8_1475_s01.log"
            prefix = "Asset Pipeline Refresh before capture\n"
            window = "Clean proof window\nH8_CAPTURE_DONE\n"
            log.write_text(prefix + window, encoding="utf-8")
            log_time = datetime(2026, 6, 4, 18, 2, tzinfo=timezone.utc)
            os.utime(log, (log_time.timestamp(), log_time.timestamp()))
            manifest["log_sha256"] = sha(log)
            manifest["log_window_start_offset"] = len(prefix)
            manifest["log_window_end_offset"] = len(prefix) + len(window)
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 0, payload["rejectCodes"])
            self.assertEqual(payload["logGate"]["scanMode"], "offset_window")

    def test_rejects_dirty_token_inside_log_window_offsets(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            log = packet / "UnityEditor_h8_1475_s01.log"
            prefix = "Clean old text\n"
            window = "CompileScripts\n"
            log.write_text(prefix + window, encoding="utf-8")
            log_time = datetime(2026, 6, 4, 18, 2, tzinfo=timezone.utc)
            os.utime(log, (log_time.timestamp(), log_time.timestamp()))
            manifest["log_sha256"] = sha(log)
            manifest["log_window_start_offset"] = len(prefix)
            manifest["log_window_end_offset"] = len(prefix) + len(window)
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("DIRTY_LOG_COMPILE", payload["rejectCodes"])

    def test_rejects_stale_log(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            log = packet / "UnityEditor_h8_1475_s01.log"
            final_time = gate.parse_timestamp(manifest["screenshots"][-1]["file_last_write_utc"])  # type: ignore[index]
            assert final_time is not None
            stale_time = final_time - timedelta(seconds=1)
            os.utime(log, (stale_time.timestamp(), stale_time.timestamp()))
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("STALE_LOG", payload["rejectCodes"])

    def test_rejects_short_log_window(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            start = datetime(2026, 6, 4, 18, 1, tzinfo=timezone.utc)
            manifest["log_window_start_utc"] = iso(start)
            manifest["log_window_end_utc"] = iso(start + timedelta(seconds=10))
            manifest["post_capture_clean_seconds"] = 10
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("LOG_WINDOW_TOO_SHORT", payload["rejectCodes"])

    def test_rejects_binary_quality_label(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, manifest = self.build_valid_packet(Path(tmp))
            manifest["global_quality_label"] = "High"
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("BINARY_QUALITY_LABEL", payload["rejectCodes"])

    def test_rejects_screenshot_under_assets(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            root = Path(tmp)
            packet, manifest = self.build_valid_packet(root)
            bad = root / "Assets" / "Screenshots" / "01_surface_coast_aegir_ui_off.png"
            write_png_header(bad)
            mtime = gate.parse_timestamp(manifest["screenshots"][0]["file_last_write_utc"])  # type: ignore[index]
            assert mtime is not None
            os.utime(bad, (mtime.timestamp(), mtime.timestamp()))
            manifest["screenshots"][0]["file_path"] = str(bad)  # type: ignore[index]
            manifest["screenshots"][0]["sha256"] = sha(bad)  # type: ignore[index]
            manifest["screenshots"][0]["byte_size"] = bad.stat().st_size  # type: ignore[index]
            self.rewrite_manifest(packet, manifest)
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("SCREENSHOT_UNDER_ASSETS", payload["rejectCodes"])

    def test_rejects_screenshot_meta_sibling(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            packet, _ = self.build_valid_packet(Path(tmp))
            meta = packet / "screenshots" / "01_surface_coast_aegir_ui_off.png.meta"
            meta.write_text("fileFormatVersion: 2\n", encoding="utf-8")
            code, payload = self.validate(packet)
            self.assertEqual(code, 1)
            self.assertIn("SCREENSHOT_META_SIBLING", payload["rejectCodes"])

    def test_cli_writes_reports(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_proof_gate_") as tmp:
            root = Path(tmp)
            packet, _ = self.build_valid_packet(root)
            json_out = root / "gate.json"
            md_out = root / "gate.md"
            code = gate.main(
                [
                    "--packet-root",
                    str(packet),
                    "--packet-id",
                    "h8_1475",
                    "--session-id",
                    "s01",
                    "--expected-quality",
                    "q060",
                    "--json-out",
                    str(json_out),
                    "--md-out",
                    str(md_out),
                    "--strict",
                ]
            )
            self.assertEqual(code, 0)
            self.assertTrue(json_out.exists())
            self.assertTrue(md_out.exists())


if __name__ == "__main__":
    unittest.main()
