#!/usr/bin/env python3
"""Validate a HECTON-8 visual proof packet before human review.

Evidence class: STATIC_FILESYSTEM / STATIC_MANIFEST / STATIC_LOG.
This tool never launches Unity, enters Play Mode, profiles, judges visual taste,
or upgrades a packet to runtime or visual acceptance.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import sys
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
SCHEMA = "hecton8.proof_packet_gate.v1"
PASS_STATUS = "PASS_STATIC_GATE"
REJECT_STATUS = "REJECTED_STATIC_GATE"
PENDING_STATUS = "PENDING_STATIC_GATE"
MIN_WIDTH = 1280
MIN_HEIGHT = 720
PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
BINARY_QUALITY_LABELS = {"low", "medium", "high", "ultra"}
PLAYER_CAPTURE_CLAIM_FIELDS = (
    "mayClaimPlayerCaptureVerified",
    "may_claim_player_capture_verified",
    "playerCaptureVerified",
    "player_capture_verified",
    "playerCaptureVerification",
    "player_capture_verification",
)
PLAYER_CAPTURE_DISPOSITIONS = {
    "PLAYER_CAPTURE_VERIFIED",
    "PLAYER-CAPTURE VERIFIED",
    "PLAYER_CAPTURE_ACCEPTED",
}

REQUIRED_VIEWS: tuple[tuple[int, str, str], ...] = (
    (1, "surface_coast_aegir_ui_off", "01_surface_coast_aegir_ui_off.png"),
    (2, "shoreline_close_1m", "02_shoreline_close_1m.png"),
    (3, "underwater_0_5m", "03_underwater_0_5m.png"),
    (4, "underwater_20_50m_route", "04_underwater_20_50m_route.png"),
    (5, "aegir_celestial_long", "05_aegir_celestial_long.png"),
    (6, "regression_low_oblique", "06_regression_low_oblique.png"),
)

REQUIRED_MANIFEST_FIELDS = (
    "schema_name",
    "schema_version",
    "harness_name",
    "harness_version",
    "packet_id",
    "session_id",
    "created_utc",
    "created_local",
    "active_scene",
    "evidence_class",
    "final_disposition",
    "may_submit_as_runtime_proof",
    "global_quality_weight",
    "global_quality_label",
    "route_owner_name",
    "route_session_id",
    "camera_source",
    "ui_policy",
    "log_path",
    "post_capture_clean_seconds",
    "screenshots",
)

REQUIRED_DERIVED_CHECKS = (
    "all_required_views_present",
    "all_required_views_unique",
    "all_required_views_have_sha256",
    "all_production_views_ui_policy_pass",
    "all_depth_predicates_pass",
    "all_route_predicates_pass",
    "quality_weight_is_continuous_float",
    "post_capture_log_window_clean",
    "manifest_written_after_final_screenshot",
    "log_last_write_after_final_screenshot",
    "screenshots_outside_assets_folder",
    "no_asset_import_dependency",
)

REQUIRED_SCREENSHOT_FIELDS = (
    "view_index",
    "view_id",
    "production_view",
    "diagnostic_view",
    "file_path",
    "file_name",
    "sha256",
    "byte_size",
    "png_width",
    "png_height",
    "capture_requested_utc",
    "file_created_utc",
    "file_last_write_utc",
    "capture_source",
    "camera_name",
    "camera_position_world",
    "camera_rotation_euler",
    "field_of_view_degrees",
    "route_anchor_id",
    "route_state_id",
    "route_state_hash",
    "route_predicate_pass",
    "route_predicate_failures",
    "camera_visual_depth_meters",
    "depth_zone_id",
    "depth_zone_name",
    "depth_zone_hash",
    "depth_predicate_pass",
    "underwater_active",
    "global_quality_weight",
    "global_quality_label",
    "render_scale_current",
    "render_scale_target",
    "post_stack_hash",
    "ui_visible",
    "log_offset_or_timestamp_at_capture",
)

FORBIDDEN_LOG_TOKENS = (
    ("DIRTY_LOG_ERROR", "Error"),
    ("DIRTY_LOG_ERROR", "Exception"),
    ("DIRTY_LOG_WARNING", "Warning"),
    ("DIRTY_LOG_ERROR", "LogError"),
    ("DIRTY_LOG_LEAK", "Found 1 leak"),
    ("DIRTY_LOG_LEAK", "Leak Detected"),
    ("DIRTY_LOG_ERROR", "shader error"),
    ("DIRTY_LOG_ERROR", "material error"),
    ("DIRTY_LOG_IMPORT", "not valid. Loading of assembly skipped"),
    ("DIRTY_LOG_COMPILE", "CompileScripts"),
    ("DIRTY_LOG_IMPORT", "Asset Pipeline Refresh"),
    ("DIRTY_LOG_DOMAIN_RELOAD", "Domain Reload"),
    ("DIRTY_LOG_DOMAIN_RELOAD", "ReloadAssembly"),
    ("DIRTY_LOG_ILPP", "ILPP"),
    ("DIRTY_LOG_ILPP", "PostProcessing ILPP"),
    ("DIRTY_LOG_ERROR", "H8_PLAYMODE_EXIT"),
    ("DIRTY_LOG_WARNING", "forced"),
    ("DIRTY_LOG_ERROR", "Access token is unavailable"),
    ("DIRTY_LOG_MCP_TRANSPORT", "MCP WebSocket connection failed"),
    ("DIRTY_LOG_MCP_TRANSPORT", "failed to start MCP transport"),
    ("DIRTY_LOG_ERROR", "ready lock"),
    ("DIRTY_LOG_IMPORT", "Library/PackageCache"),
    ("DIRTY_LOG_IMPORT", "AssetDatabase.Refresh"),
    ("DIRTY_LOG_IMPORT", "RefreshInfo"),
)


@dataclass
class GateResult:
    status: str = PASS_STATUS
    reject_codes: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    required_views: dict[str, Any] = field(default_factory=dict)
    manifest_gate: dict[str, Any] = field(default_factory=dict)
    screenshot_gate: dict[str, Any] = field(default_factory=dict)
    log_gate: dict[str, Any] = field(default_factory=dict)
    contamination_gate: dict[str, Any] = field(default_factory=dict)
    freshness_gate: dict[str, Any] = field(default_factory=dict)

    def reject(self, code: str, detail: str | None = None) -> None:
        if code not in self.reject_codes:
            self.reject_codes.append(code)
        if detail:
            self.warnings.append(f"{code}: {detail}")

    @property
    def passed(self) -> bool:
        return not self.reject_codes and self.status == PASS_STATUS


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def parse_timestamp(value: Any) -> datetime | None:
    if value is None:
        return None
    text = str(value).strip()
    if not text:
        return None
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(text)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def fs_mtime_utc(path: Path) -> datetime:
    return datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc)


def normalize_path(path: Path, root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def resolve_project_path(raw_path: Any, packet_root: Path) -> Path:
    path = Path(str(raw_path))
    if path.is_absolute():
        return path
    candidate = (packet_root / path).resolve()
    if candidate.exists():
        return candidate
    return (REPO_ROOT / path).resolve()


def is_relative_to(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except ValueError:
        return False


def is_under_assets(path: Path) -> bool:
    if is_relative_to(path, REPO_ROOT / "Assets"):
        return True
    return any(part.lower() == "assets" for part in path.resolve().parts)


def read_png_dimensions(path: Path) -> tuple[int, int]:
    data = path.read_bytes()
    if len(data) < 24 or not data.startswith(PNG_MAGIC) or data[12:16] != b"IHDR":
        raise ValueError("invalid PNG magic/IHDR")
    return struct.unpack(">II", data[16:24])


def load_manifest(path: Path) -> tuple[dict[str, Any] | None, str | None]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        return None, f"manifest JSON malformed: {exc}"
    if not isinstance(payload, dict):
        return None, "manifest root must be an object"
    return payload, None


def read_manifest_sha(path: Path) -> str:
    if not path.exists():
        return ""
    text = path.read_text(encoding="utf-8", errors="ignore").strip()
    match = re.search(r"\b([0-9a-fA-F]{64})\b", text)
    return match.group(1).lower() if match else ""


def is_non_empty_string(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def is_true(value: Any) -> bool:
    return value is True


def validate_manifest(
    manifest: dict[str, Any],
    manifest_path: Path,
    packet_id: str,
    session_id: str,
    expected_quality: str | None,
    min_clean_seconds: int,
    result: GateResult,
) -> None:
    result.manifest_gate["path"] = normalize_path(manifest_path)
    missing = [field for field in REQUIRED_MANIFEST_FIELDS if field not in manifest]
    if missing:
        result.reject("MANIFEST_FIELD_MISSING", ", ".join(missing))

    if manifest.get("packet_id") != packet_id:
        result.reject("PACKET_ID_MISMATCH", str(manifest.get("packet_id")))
    if manifest.get("session_id") != session_id:
        result.reject("SESSION_ID_MISMATCH", str(manifest.get("session_id")))

    quality_weight = manifest.get("global_quality_weight")
    if not isinstance(quality_weight, (int, float)) or not 0.0 <= float(quality_weight) <= 1.0:
        result.reject("QUALITY_WEIGHT_INVALID", repr(quality_weight))

    quality_label = str(manifest.get("global_quality_label", "")).strip()
    if quality_label.lower() in BINARY_QUALITY_LABELS or not re.fullmatch(r"q\d{3}", quality_label):
        result.reject("BINARY_QUALITY_LABEL", quality_label)
    if expected_quality and quality_label != expected_quality:
        result.reject("QUALITY_LABEL_MISMATCH", f"{quality_label} != {expected_quality}")

    if manifest.get("final_disposition") == "ACCEPTED_BY_HARNESS" and manifest.get("may_submit_as_runtime_proof") is not True:
        result.reject("MANIFEST_ACCEPTANCE_CONFLICT", "accepted disposition without runtime-proof flag")

    validate_player_capture_overclaim(manifest, result)

    checks = manifest.get("derived_checks", {})
    if not isinstance(checks, dict):
        result.reject("DERIVED_CHECKS_MISSING", "derived_checks must be an object")
        checks = {}
    for check_name in REQUIRED_DERIVED_CHECKS:
        if checks.get(check_name) is not True:
            result.reject("DERIVED_CHECK_FALSE", check_name)

    post_clean = manifest.get("post_capture_clean_seconds")
    if not isinstance(post_clean, (int, float)) or float(post_clean) < min_clean_seconds:
        result.reject("LOG_WINDOW_TOO_SHORT", repr(post_clean))

    screenshots = manifest.get("screenshots")
    if not isinstance(screenshots, list):
        result.reject("SCREENSHOTS_FIELD_INVALID", "screenshots must be a list")


def is_player_capture_claim_value(value: Any) -> bool:
    return value is not None and value is not False


def validate_player_capture_overclaim(manifest: dict[str, Any], result: GateResult) -> None:
    """Reject manifest attempts to promote a static packet into player-capture truth."""
    for field_name in PLAYER_CAPTURE_CLAIM_FIELDS:
        if is_player_capture_claim_value(manifest.get(field_name)):
            result.reject("PLAYER_CAPTURE_CLAIM_UNSUPPORTED", field_name)

    disposition = str(manifest.get("final_disposition", "")).strip().upper()
    if disposition in PLAYER_CAPTURE_DISPOSITIONS:
        result.reject("PLAYER_CAPTURE_CLAIM_UNSUPPORTED", "final_disposition")

    checks = manifest.get("derived_checks")
    if isinstance(checks, dict):
        for field_name in PLAYER_CAPTURE_CLAIM_FIELDS:
            if is_player_capture_claim_value(checks.get(field_name)):
                result.reject("PLAYER_CAPTURE_CLAIM_UNSUPPORTED", f"derived_checks.{field_name}")


def validate_manifest_checksum(manifest_path: Path, result: GateResult) -> None:
    sha_path = manifest_path.with_suffix(manifest_path.suffix + ".sha256")
    if not sha_path.exists():
        sha_path = manifest_path.parent / "manifest.sha256"
    if not sha_path.exists():
        result.reject("MANIFEST_SHA_MISSING", "manifest.sha256 missing")
        return
    expected = read_manifest_sha(sha_path)
    actual = sha256_file(manifest_path)
    result.manifest_gate["manifestSha256"] = actual
    if not expected:
        result.reject("MANIFEST_SHA_MALFORMED", normalize_path(sha_path))
    elif expected != actual:
        result.reject("MANIFEST_SHA_MISMATCH", f"{expected} != {actual}")


def validate_screenshots(
    manifest: dict[str, Any],
    packet_root: Path,
    packet_id: str,
    session_id: str,
    expected_quality: str | None,
    strict: bool,
    result: GateResult,
) -> datetime | None:
    screenshots = manifest.get("screenshots")
    if not isinstance(screenshots, list):
        return None

    by_view: dict[str, dict[str, Any]] = {}
    production_files: set[str] = set()
    final_screenshot_time: datetime | None = None

    for record in screenshots:
        if not isinstance(record, dict):
            result.reject("SCREENSHOT_RECORD_INVALID", "record must be object")
            continue
        missing = [field for field in REQUIRED_SCREENSHOT_FIELDS if field not in record]
        if missing:
            result.reject("SCREENSHOT_FIELD_MISSING", f"{record.get('view_id')}: {', '.join(missing)}")

        view_id = str(record.get("view_id", "")).strip()
        if view_id in by_view:
            result.reject("DUPLICATE_VIEW", view_id)
        by_view[view_id] = record

        file_path = resolve_project_path(record.get("file_path", ""), packet_root)
        file_name = str(record.get("file_name", "")).strip()
        result.required_views.setdefault(view_id or "<missing>", {})["path"] = normalize_path(file_path)

        if not file_path.exists():
            result.reject("SCREENSHOT_MISSING", normalize_path(file_path))
            continue
        if not is_relative_to(file_path, packet_root):
            result.reject("SCREENSHOT_OUTSIDE_PACKET", normalize_path(file_path))
        if is_under_assets(file_path):
            result.reject("SCREENSHOT_UNDER_ASSETS", normalize_path(file_path))
        if file_path.suffix.lower() != ".png":
            result.reject("SCREENSHOT_NOT_PNG", normalize_path(file_path))
        if file_path.with_suffix(file_path.suffix + ".meta").exists():
            result.reject("SCREENSHOT_META_SIBLING", normalize_path(file_path))
        if file_name and file_path.name != file_name:
            result.reject("SCREENSHOT_FILENAME_MISMATCH", f"{file_path.name} != {file_name}")

        try:
            width, height = read_png_dimensions(file_path)
        except ValueError as exc:
            result.reject("PNG_INVALID", f"{normalize_path(file_path)}: {exc}")
            continue
        if width != record.get("png_width") or height != record.get("png_height"):
            result.reject("PNG_DIMENSION_MISMATCH", f"{file_path.name}: {width}x{height}")
        if width < MIN_WIDTH or height < MIN_HEIGHT:
            result.reject("PNG_DIMENSION_TOO_SMALL", f"{file_path.name}: {width}x{height}")

        actual_size = file_path.stat().st_size
        if actual_size != record.get("byte_size"):
            result.reject("PNG_BYTE_SIZE_MISMATCH", f"{file_path.name}: {actual_size}")
        actual_sha = sha256_file(file_path)
        if actual_sha != str(record.get("sha256", "")).lower():
            result.reject("PNG_SHA256_MISMATCH", file_path.name)

        fs_time = fs_mtime_utc(file_path)
        manifest_time = parse_timestamp(record.get("file_last_write_utc"))
        if manifest_time is None:
            result.reject("SCREENSHOT_TIMESTAMP_INVALID", file_path.name)
        elif abs((fs_time - manifest_time).total_seconds()) > 5:
            result.reject("SCREENSHOT_TIMESTAMP_MISMATCH", file_path.name)
        if manifest_time and (final_screenshot_time is None or manifest_time > final_screenshot_time):
            final_screenshot_time = manifest_time

        if record.get("packet_id", packet_id) != packet_id:
            result.reject("SCREENSHOT_PACKET_ID_MISMATCH", view_id)
        if record.get("session_id", session_id) != session_id:
            result.reject("SCREENSHOT_SESSION_ID_MISMATCH", view_id)

        label = str(record.get("global_quality_label", "")).strip()
        if label.lower() in BINARY_QUALITY_LABELS or not re.fullmatch(r"q\d{3}", label):
            result.reject("BINARY_QUALITY_LABEL", f"{view_id}: {label}")
        if expected_quality and label != expected_quality:
            result.reject("QUALITY_LABEL_MISMATCH", f"{view_id}: {label}")
        weight = record.get("global_quality_weight")
        if not isinstance(weight, (int, float)) or not 0.0 <= float(weight) <= 1.0:
            result.reject("QUALITY_WEIGHT_INVALID", f"{view_id}: {weight!r}")

        if record.get("production_view") is True:
            production_files.add(file_path.name)

    for index, view_id, filename in REQUIRED_VIEWS:
        record = by_view.get(view_id)
        if record is None:
            result.reject("REQUIRED_VIEW_MISSING", view_id)
            continue
        result.required_views[view_id] = {
            "index": index,
            "expectedFile": filename,
            "actualFile": record.get("file_name"),
            "production": record.get("production_view"),
            "diagnostic": record.get("diagnostic_view"),
            "routePredicatePass": record.get("route_predicate_pass"),
            "depthPredicatePass": record.get("depth_predicate_pass"),
        }
        if record.get("view_index") != index:
            result.reject("VIEW_INDEX_MISMATCH", view_id)
        if record.get("file_name") != filename:
            result.reject("FALSE_ROUTE_LABEL", f"{view_id}: {record.get('file_name')} != {filename}")
        if record.get("production_view") is not True:
            result.reject("PRODUCTION_VIEW_MISSING", view_id)
        if record.get("diagnostic_view") is True:
            result.reject("DIAGNOSTIC_SUBSTITUTION", view_id)
        if record.get("route_predicate_pass") is not True:
            result.reject("ROUTE_PREDICATE_FAIL", view_id)
        if record.get("depth_predicate_pass") is not True:
            result.reject("DEPTH_PREDICATE_FAIL", view_id)
        validate_view_specific_predicates(view_id, record, result)

    if strict:
        screenshot_pngs = sorted((packet_root / "screenshots").glob("*.png"))
        known_files = {filename for _, _, filename in REQUIRED_VIEWS}
        if any(path.name not in known_files and path.name not in production_files for path in screenshot_pngs):
            result.reject("UNKNOWN_SCREENSHOT_FILE", ", ".join(path.name for path in screenshot_pngs if path.name not in known_files))

    return final_screenshot_time


def validate_view_specific_predicates(view_id: str, record: dict[str, Any], result: GateResult) -> None:
    depth = record.get("camera_visual_depth_meters")
    if view_id == "underwater_0_5m":
        if not isinstance(depth, (int, float)) or not 0.25 <= float(depth) <= 5.0:
            result.reject("DEPTH_PREDICATE_FAIL", f"{view_id}: {depth!r}")
        if record.get("underwater_active") is not True:
            result.reject("UNDERWATER_INACTIVE", view_id)
    elif view_id == "underwater_20_50m_route":
        if not isinstance(depth, (int, float)) or not 20.0 <= float(depth) <= 50.0:
            result.reject("DEPTH_PREDICATE_FAIL", f"{view_id}: {depth!r}")
        if record.get("underwater_active") is not True:
            result.reject("UNDERWATER_INACTIVE", view_id)
    elif view_id == "surface_coast_aegir_ui_off":
        if record.get("ui_visible") is not False:
            result.reject("UI_POLICY_FAIL", view_id)
    elif view_id == "shoreline_close_1m":
        if not is_non_empty_string(record.get("route_anchor_id")):
            result.reject("FALSE_ROUTE_LABEL", "shoreline view lacks route_anchor_id")
    elif view_id in {"aegir_celestial_long", "regression_low_oblique"}:
        if not is_non_empty_string(record.get("route_anchor_id")):
            result.reject("FALSE_ROUTE_LABEL", f"{view_id} lacks route_anchor_id")


def validate_freshness(
    manifest: dict[str, Any],
    manifest_path: Path,
    final_screenshot_time: datetime | None,
    result: GateResult,
) -> None:
    created_utc = parse_timestamp(manifest.get("created_utc"))
    if created_utc is None:
        result.reject("MANIFEST_TIMESTAMP_INVALID", "created_utc")
    if final_screenshot_time is not None and created_utc is not None and created_utc <= final_screenshot_time:
        result.reject("MANIFEST_STALE", "created_utc predates final screenshot")
    if final_screenshot_time is not None and fs_mtime_utc(manifest_path) <= final_screenshot_time:
        result.reject("MANIFEST_STALE", "manifest file mtime predates final screenshot")
    result.freshness_gate["finalScreenshotUtc"] = final_screenshot_time.isoformat() if final_screenshot_time else None
    result.freshness_gate["manifestCreatedUtc"] = created_utc.isoformat() if created_utc else None


def validate_log(
    manifest: dict[str, Any],
    packet_root: Path,
    final_screenshot_time: datetime | None,
    min_clean_seconds: int,
    result: GateResult,
) -> None:
    log_path = resolve_project_path(manifest.get("log_path", ""), packet_root)
    result.log_gate["path"] = normalize_path(log_path)
    if not log_path.exists():
        result.reject("MISSING_LOG", normalize_path(log_path))
        return
    if is_under_assets(log_path):
        result.reject("LOG_UNDER_ASSETS", normalize_path(log_path))
    if final_screenshot_time and fs_mtime_utc(log_path) <= final_screenshot_time:
        result.reject("STALE_LOG", "log mtime predates final screenshot")
    log_sha = manifest.get("log_sha256")
    if isinstance(log_sha, str) and log_sha:
        actual = sha256_file(log_path)
        if actual != log_sha.lower():
            result.reject("LOG_SHA256_MISMATCH", normalize_path(log_path))

    window_start = parse_timestamp(manifest.get("log_window_start_utc"))
    window_end = parse_timestamp(manifest.get("log_window_end_utc"))
    if window_start and window_end:
        seconds = (window_end - window_start).total_seconds()
        if seconds < min_clean_seconds:
            result.reject("LOG_WINDOW_TOO_SHORT", str(seconds))
    elif not isinstance(manifest.get("post_capture_clean_seconds"), (int, float)):
        result.reject("LOG_WINDOW_MISSING", "no timestamps or post_capture_clean_seconds")

    text = read_log_window_text(log_path, manifest, result)
    for code, token in FORBIDDEN_LOG_TOKENS:
        if token in text:
            result.reject(code, token)
    result.log_gate["lastWriteUtc"] = fs_mtime_utc(log_path).isoformat()


def read_log_window_text(log_path: Path, manifest: dict[str, Any], result: GateResult) -> str:
    text = log_path.read_text(encoding="utf-8", errors="ignore")
    start = manifest.get("log_window_start_offset")
    end = manifest.get("log_window_end_offset")
    if isinstance(start, int) and isinstance(end, int):
        if start < 0 or end < start or end > len(text):
            result.reject("LOG_WINDOW_OFFSET_INVALID", f"{start}:{end} for {len(text)} bytes")
            return text
        result.log_gate["scanMode"] = "offset_window"
        result.log_gate["scanStartOffset"] = start
        result.log_gate["scanEndOffset"] = end
        return text[start:end]
    result.log_gate["scanMode"] = "full_file"
    result.warnings.append("LOG_WINDOW_SCAN_FULL_FILE: no log offsets; scanned full log.")
    return text


def validate_contamination(packet_root: Path, manifest: dict[str, Any], result: GateResult) -> None:
    if is_under_assets(packet_root):
        result.reject("PACKET_UNDER_ASSETS", normalize_path(packet_root))
    if (REPO_ROOT / "Assets" / "Screenshots").exists():
        result.warnings.append("Assets/Screenshots exists; packet must not use it.")
    if manifest.get("derived_checks", {}).get("no_asset_import_dependency") is not True:
        result.reject("ASSET_IMPORT_DEPENDENCY", "no_asset_import_dependency false or missing")
    result.contamination_gate["packetRoot"] = normalize_path(packet_root)


def build_payload(
    result: GateResult,
    packet_root: Path,
    packet_id: str,
    session_id: str,
) -> dict[str, Any]:
    status = PASS_STATUS if not result.reject_codes else REJECT_STATUS
    return {
        "schema": SCHEMA,
        "status": status,
        "evidenceClass": "STATIC_FILESYSTEM",
        "maySubmitForHumanVisualReview": status == PASS_STATUS,
        "mayClaimPlayerCaptureVerified": False,
        "playerCaptureClaimPolicy": "STATIC_GATE_NEVER_VERIFIES_PLAYER_CAPTURE",
        "packetRoot": normalize_path(packet_root),
        "packetId": packet_id,
        "sessionId": session_id,
        "rejectCodes": result.reject_codes,
        "warnings": result.warnings,
        "requiredViews": result.required_views,
        "manifestGate": result.manifest_gate,
        "screenshotGate": result.screenshot_gate,
        "logGate": result.log_gate,
        "contaminationGate": result.contamination_gate,
        "freshnessGate": result.freshness_gate,
    }


def write_json_report(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")


def write_markdown_report(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# HECTON Proof Packet Gate",
        "",
        f"Status: `{payload['status']}`",
        f"Packet: `{payload['packetId']}` / `{payload['sessionId']}`",
        f"Root: `{payload['packetRoot']}`",
        "",
        "This is a static packet gate only. It is not runtime, profiler, or visual acceptance.",
        "",
        "## Reject Codes",
    ]
    reject_codes = payload.get("rejectCodes") or []
    if reject_codes:
        lines.extend(f"- `{code}`" for code in reject_codes)
    else:
        lines.append("- none")
    lines.append("")
    lines.append("## Warnings")
    warnings = payload.get("warnings") or []
    if warnings:
        lines.extend(f"- {warning}" for warning in warnings)
    else:
        lines.append("- none")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def validate_packet(args: argparse.Namespace) -> tuple[int, dict[str, Any]]:
    packet_root = Path(args.packet_root)
    if not packet_root.is_absolute():
        packet_root = (REPO_ROOT / packet_root).resolve()
    if not packet_root.exists() or not packet_root.is_dir():
        raise ValueError(f"packet root missing or not a directory: {packet_root}")

    result = GateResult()
    manifest_path = packet_root / "manifest.json"
    if not manifest_path.exists():
        pngs = list(packet_root.rglob("*.png"))
        result.reject("RAW_PNG_SET" if pngs else "MISSING_MANIFEST", "manifest.json missing")
        payload = build_payload(result, packet_root, args.packet_id, args.session_id)
        return 1, payload

    manifest, manifest_error = load_manifest(manifest_path)
    if manifest is None:
        result.reject("MANIFEST_MALFORMED", manifest_error)
        payload = build_payload(result, packet_root, args.packet_id, args.session_id)
        return 1, payload

    validate_manifest_checksum(manifest_path, result)
    validate_manifest(
        manifest,
        manifest_path,
        args.packet_id,
        args.session_id,
        args.expected_quality,
        args.min_post_capture_clean_seconds,
        result,
    )
    validate_contamination(packet_root, manifest, result)
    final_screenshot_time = validate_screenshots(
        manifest,
        packet_root,
        args.packet_id,
        args.session_id,
        args.expected_quality,
        args.strict,
        result,
    )
    validate_freshness(manifest, manifest_path, final_screenshot_time, result)
    validate_log(manifest, packet_root, final_screenshot_time, args.min_post_capture_clean_seconds, result)

    payload = build_payload(result, packet_root, args.packet_id, args.session_id)
    return (0 if payload["status"] == PASS_STATUS else 1), payload


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet-root", required=True)
    parser.add_argument("--packet-id", required=True)
    parser.add_argument("--session-id", required=True)
    parser.add_argument("--expected-quality")
    parser.add_argument("--min-post-capture-clean-seconds", type=int, default=60)
    parser.add_argument("--json-out")
    parser.add_argument("--md-out")
    parser.add_argument("--strict", action="store_true")
    parser.add_argument("--allow-diagnostic-view", action="store_true")
    parser.add_argument("--forbidden-token-profile", default="visual_proof_v1")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.forbidden_token_profile != "visual_proof_v1":
        print("unsupported forbidden token profile", file=sys.stderr)
        return 2

    try:
        exit_code, payload = validate_packet(args)
    except ValueError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    except Exception as exc:  # pragma: no cover - last-resort CLI guard.
        print(f"internal validator error: {exc}", file=sys.stderr)
        return 3

    if args.json_out:
        json_path = Path(args.json_out)
        if not json_path.is_absolute():
            json_path = REPO_ROOT / json_path
        write_json_report(json_path, payload)
    if args.md_out:
        md_path = Path(args.md_out)
        if not md_path.is_absolute():
            md_path = REPO_ROOT / md_path
        write_markdown_report(md_path, payload)

    print(payload["status"])
    if payload["rejectCodes"]:
        for code in payload["rejectCodes"]:
            print(code)
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
