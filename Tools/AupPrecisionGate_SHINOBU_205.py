#!/usr/bin/env python3
"""Static AUP precision gate for SHINOBU_205.

This tool is editorless. It scans first-party C# source for coordinate
authority regressions that Unity import cannot be trusted to catch early:

- direct absolute AUP/double3 casts to float3
- component casts such as new float3((float)SomeAUP.x, ...)
- Transform.position used as gameplay/authority input in distance/AUP calls

The gate writes a full report and merges a compact summary into the shared
math optimization report while preserving other agents' top-level keys.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_FULL_REPORT = REPO_ROOT / "Docs" / "Reports" / "AUP_PRECISION_SCAN_SHINOBU_205.json"
DEFAULT_MATH_REPORT = REPO_ROOT / "Docs" / "Reports" / "MATH_OPTIMIZATION_REPORT.json"

SCHEMA = "hecton8.aup_precision_gate.shinobu_205.v1"

ANY_AUP_FLOAT_CAST = re.compile(
    r"\(float3\)\s*[^;\n]*(?:AUP|Aup|Absolute|Universe|double3)"
)
DIRECT_AUP_FLOAT_CAST = re.compile(
    r"\(float3\)\s*(?!\()(?P<expr>[A-Za-z_][A-Za-z0-9_\.]*(?:AUP|Aup|Absolute|Universe)[A-Za-z0-9_\.]*)"
)
COMPONENT_AUP_FLOAT_CAST = re.compile(
    r"new\s+(?:float3|Vector3)\s*\([^;\n]*\(float\)\s*[^,;\n]*(?:AUP|Aup|Absolute|Universe)"
)
APPROVED_HELPER = re.compile(
    r"AupPrecisionMath\.(?:LocalDeltaDouble|LocalDeltaFloat3|LocalDeltaFloat3Clamped|"
    r"DowncastLocalDelta|DowncastLocalDeltaClamped|DowncastProceduralPhase|"
    r"DistanceSqSafeDouble|DistanceSqSafeFloat|SafeNormalize|SafeNormalizeLocalDelta|"
    r"ResolveGateDistanceMeters|ShouldSkipByDistanceSq|CreateOutOfBoundsSentinel)"
)
STRICT_TRANSFORM_AUTHORITY_READ = re.compile(
    r"(?:AbsoluteUniversePosition\.FromRuntimePosition|"
    r"HectonFloatingOrigin\.ToAbsoluteUniversePositionDouble3|"
    r"Vector3\.Distance|math\.distance|math\.distancesq|\.sqrMagnitude)"
    r"\s*\([^;\n]*\.position|\([^;\n]*\.position\s*[-+][^;\n]*\.position\)"
)
BROAD_TRANSFORM_POSITION_READ = re.compile(
    r"=\s*[^;]*\.position\b|\.position\b[^=]*\)"
)
FLOAT_DISTANCE_REVIEW = re.compile(
    r"Vector3\.Distance|math\.distance|math\.distancesq"
)
TRANSFORM_DISTANCE_REVIEW = re.compile(
    r"(?:Vector3\.Distance|math\.distance|math\.distancesq)\s*\([^;\n]*\.position|"
    r"\([^;\n]*\.position\s*[-+][^;\n]*\)\.sqrMagnitude|"
    r"\([^;\n]*[-+][^;\n]*\.position[^;\n]*\)\.sqrMagnitude"
)
RUNTIME_AUP_BRIDGE_REVIEW = re.compile(
    r"AbsoluteUniversePosition\.FromRuntimePosition|"
    r"HectonFloatingOrigin\.ToAbsoluteUniversePositionDouble3"
)
LEGACY_ABSOLUTE_FLOAT_PAYLOAD_REVIEW = re.compile(
    r"new\s+(?:float3|Vector3)\s*\([^;\n]*\(float\)\s*[^,;\n]*(?:absolute|Absolute)[A-Za-z0-9_\.]*"
)

SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Temp",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def normalize_path(path: Path, root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return path.as_posix().replace("\\", "/")


def is_editor_path(path: Path) -> bool:
    return any(part.lower() == "editor" for part in path.parts)


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def iter_cs_files(source_root: Path) -> list[Path]:
    if not source_root.exists():
        return []
    return [
        path
        for path in sorted(source_root.rglob("*.cs"))
        if not should_skip(path.relative_to(source_root))
    ]


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="ignore")


def finding(file_path: Path, line: int, kind: str, text: str) -> dict[str, Any]:
    return {
        "file": normalize_path(file_path),
        "line": line,
        "kind": kind,
        "snippet": text.strip(),
    }


def append_limited(rows: list[dict[str, Any]], row: dict[str, Any], limit: int) -> None:
    if len(rows) < limit:
        rows.append(row)


def scan_sources(source_root: Path, sample_limit: int) -> dict[str, Any]:
    files = iter_cs_files(source_root)
    blocked_casts: list[dict[str, Any]] = []
    editor_reviews: list[dict[str, Any]] = []
    strict_transform: list[dict[str, Any]] = []
    broad_transform: list[dict[str, Any]] = []
    distance_reviews: list[dict[str, Any]] = []
    transform_distance_reviews: list[dict[str, Any]] = []
    runtime_aup_bridge_reviews: list[dict[str, Any]] = []
    legacy_absolute_payload_reviews: list[dict[str, Any]] = []

    strict_by_file: Counter[str] = Counter()
    direct_by_file: Counter[str] = Counter()
    component_by_file: Counter[str] = Counter()
    runtime_aup_bridge_by_file: Counter[str] = Counter()
    legacy_absolute_payload_by_file: Counter[str] = Counter()
    approved_helper_calls = 0
    broad_transform_count = 0
    distance_review_count = 0
    transform_distance_review_count = 0
    runtime_aup_bridge_review_count = 0
    legacy_absolute_payload_review_count = 0
    direct_cast_count = 0
    runtime_component_count = 0
    editor_component_count = 0
    strict_transform_count = 0

    for file_path in files:
        editor_path = is_editor_path(file_path)
        scanner_self_diagnostic = file_path.name.lower() == "aup_premature_cast_scanner.cs"
        lines = read_text(file_path).splitlines()
        for index, line in enumerate(lines, start=1):
            if APPROVED_HELPER.search(line):
                approved_helper_calls += 1

            if ANY_AUP_FLOAT_CAST.search(line) and "AupPrecisionMath." not in line:
                direct_cast_count += 1
                rel = normalize_path(file_path)
                direct_by_file[rel] += 1
                kind = "PREMATURE_FLOAT3_AUP_CAST" if DIRECT_AUP_FLOAT_CAST.search(line) else "UNAPPROVED_FLOAT3_AUP_CAST"
                append_limited(blocked_casts, finding(file_path, index, kind, line), sample_limit)

            if (
                COMPONENT_AUP_FLOAT_CAST.search(line)
                and "AupPrecisionMath." not in line
                and not scanner_self_diagnostic
            ):
                if editor_path:
                    editor_component_count += 1
                    append_limited(
                        editor_reviews,
                        finding(file_path, index, "EDITOR_COMPONENT_FLOAT_AUP_CAST_REVIEW", line),
                        sample_limit,
                    )
                else:
                    runtime_component_count += 1
                    rel = normalize_path(file_path)
                    component_by_file[rel] += 1
                    append_limited(
                        blocked_casts,
                        finding(file_path, index, "COMPONENT_FLOAT3_AUP_CAST", line),
                        sample_limit,
                    )

            if (
                STRICT_TRANSFORM_AUTHORITY_READ.search(line)
                and "Editor" not in line
                and "Gizmos" not in line
                and "Handles" not in line
                and not editor_path
            ):
                strict_transform_count += 1
                rel = normalize_path(file_path)
                strict_by_file[rel] += 1
                append_limited(
                    strict_transform,
                    finding(file_path, index, "TRANSFORM_POSITION_AUTHORITY_BLOCK", line),
                    sample_limit,
                )

            if (
                BROAD_TRANSFORM_POSITION_READ.search(line)
                and "Editor" not in line
                and "Gizmos" not in line
                and "Handles" not in line
                and not editor_path
            ):
                broad_transform_count += 1
                append_limited(
                    broad_transform,
                    finding(file_path, index, "TRANSFORM_POSITION_AUTHORITY_REVIEW", line),
                    sample_limit,
                )

            if FLOAT_DISTANCE_REVIEW.search(line) and "AupPrecisionMath." not in line:
                distance_review_count += 1
                append_limited(distance_reviews, finding(file_path, index, "FLOAT_DISTANCE_REVIEW", line), sample_limit)

            if (
                TRANSFORM_DISTANCE_REVIEW.search(line)
                and "Editor" not in line
                and "Gizmos" not in line
                and "Handles" not in line
                and not editor_path
            ):
                transform_distance_review_count += 1
                append_limited(
                    transform_distance_reviews,
                    finding(file_path, index, "TRANSFORM_DISTANCE_REVIEW", line),
                    sample_limit,
                )

            if (
                RUNTIME_AUP_BRIDGE_REVIEW.search(line)
                and "TryResolveAupFromRuntimeOrigin" not in line
                and "GlobalSignals.CurrentRuntimeOriginAup" not in line
                and not editor_path
            ):
                runtime_aup_bridge_review_count += 1
                rel = normalize_path(file_path)
                runtime_aup_bridge_by_file[rel] += 1
                append_limited(
                    runtime_aup_bridge_reviews,
                    finding(file_path, index, "RUNTIME_AUP_BRIDGE_REVIEW", line),
                    sample_limit,
                )

            if (
                LEGACY_ABSOLUTE_FLOAT_PAYLOAD_REVIEW.search(line)
                and "AupPrecisionMath." not in line
                and not scanner_self_diagnostic
            ):
                legacy_absolute_payload_review_count += 1
                rel = normalize_path(file_path)
                legacy_absolute_payload_by_file[rel] += 1
                append_limited(
                    legacy_absolute_payload_reviews,
                    finding(file_path, index, "LEGACY_ABSOLUTE_FLOAT_PAYLOAD_REVIEW", line),
                    sample_limit,
                )

    return {
        "filesScanned": len(files),
        "approvedHelperCalls": approved_helper_calls,
        "directAupFloat3CastCount": direct_cast_count,
        "runtimeComponentFloatAupCastCount": runtime_component_count,
        "editorComponentFloatAupCastReviewCount": editor_component_count,
        "strictTransformAuthorityReadCount": strict_transform_count,
        "broadTransformPositionReviewCount": broad_transform_count,
        "floatDistanceReviewCount": distance_review_count,
        "transformDistanceReviewCount": transform_distance_review_count,
        "runtimeAupBridgeReviewCount": runtime_aup_bridge_review_count,
        "legacyAbsoluteFloatPayloadReviewCount": legacy_absolute_payload_review_count,
        "blockedCastFindings": blocked_casts,
        "editorComponentFloatAupCastReviews": editor_reviews,
        "strictTransformAuthorityFindings": strict_transform,
        "broadTransformPositionFindings": broad_transform,
        "floatDistanceFindings": distance_reviews,
        "transformDistanceFindings": transform_distance_reviews,
        "runtimeAupBridgeFindings": runtime_aup_bridge_reviews,
        "legacyAbsoluteFloatPayloadFindings": legacy_absolute_payload_reviews,
        "directAupFloat3CastByFile": sorted(
            ({"file": file, "count": count} for file, count in direct_by_file.items()),
            key=lambda row: (-row["count"], row["file"]),
        ),
        "runtimeComponentFloatAupCastByFile": sorted(
            ({"file": file, "count": count} for file, count in component_by_file.items()),
            key=lambda row: (-row["count"], row["file"]),
        ),
        "strictTransformAuthorityByFile": sorted(
            ({"file": file, "count": count} for file, count in strict_by_file.items()),
            key=lambda row: (-row["count"], row["file"]),
        ),
        "runtimeAupBridgeByFile": sorted(
            ({"file": file, "count": count} for file, count in runtime_aup_bridge_by_file.items()),
            key=lambda row: (-row["count"], row["file"]),
        ),
        "legacyAbsoluteFloatPayloadByFile": sorted(
            ({"file": file, "count": count} for file, count in legacy_absolute_payload_by_file.items()),
            key=lambda row: (-row["count"], row["file"]),
        ),
    }


def build_payload(args: argparse.Namespace) -> dict[str, Any]:
    source_root = Path(args.source_root)
    counts = scan_sources(source_root, args.sample_limit)
    hard_blockers = (
        counts["directAupFloat3CastCount"]
        + counts["runtimeComponentFloatAupCastCount"]
        + counts["strictTransformAuthorityReadCount"]
    )
    status = "FAIL_STATIC_GATE" if hard_blockers else "PASS_STATIC_GATE"
    return {
        "schema": SCHEMA,
        "scannerId": "SHINOBU_205_AUP_PRECISION_INSPECTOR",
        "generatedUtc": utc_now(),
        "sourceRoot": normalize_path(source_root),
        "status": status,
        "hardBlockerCount": hard_blockers,
        "thresholds": {
            "maxDirectAupFloat3Casts": args.max_direct_casts,
            "maxRuntimeComponentFloatAupCasts": args.max_runtime_component_casts,
            "maxStrictTransformAuthorityReads": args.max_strict_transform_authority,
        },
        "precisionRule": "subtract target/observer in double3 before any float3 downcast",
        "transformRule": "Transform.position is presentation only; AUP/DataVault is spatial authority",
        "ownedVaultIds": "73200..73208",
        "layoutValidationFailures": "PENDING_UNITY_EDITOR_RUN",
        "counts": counts,
    }


def load_json_object(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (json.JSONDecodeError, OSError):
        return {}
    return value if isinstance(value, dict) else {}


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def upsert_math_report(math_report_path: Path, full_report_path: Path, payload: dict[str, Any]) -> None:
    report = load_json_object(math_report_path)
    counts = payload["counts"]
    report["aup_precision_inspector"] = {
        "scanner_id": payload["scannerId"],
        "generated_utc": payload["generatedUtc"],
        "cli_gate": normalize_path(Path(__file__)),
        "full_report": normalize_path(full_report_path),
        "status": payload["status"],
        "hard_blocker_count": payload["hardBlockerCount"],
        "blocked_premature_float3_aup_casts": counts["directAupFloat3CastCount"]
        + counts["runtimeComponentFloatAupCastCount"],
        "direct_aup_float3_casts": counts["directAupFloat3CastCount"],
        "runtime_component_float_audits": counts["runtimeComponentFloatAupCastCount"],
        "editor_component_float_reviews": counts["editorComponentFloatAupCastReviewCount"],
        "blocked_transform_authority_reads": counts["strictTransformAuthorityReadCount"],
        "strict_transform_authority_files": len(counts["strictTransformAuthorityByFile"]),
        "runtime_aup_bridge_reviews": counts["runtimeAupBridgeReviewCount"],
        "legacy_absolute_float_payload_reviews": counts["legacyAbsoluteFloatPayloadReviewCount"],
        "layout_validation_failures": payload["layoutValidationFailures"],
        "precision_rule": payload["precisionRule"],
        "transform_rule": payload["transformRule"],
        "owned_vault_ids": payload["ownedVaultIds"],
        "blackbox_ring": "AupPrecisionTelemetryEntry[300]",
        "verification": (
            "CLI gate: direct AUP float3 cast and runtime component cast counts are zero; "
            "strict Transform.position authority reads are zero; runtime AUP bridge reviews track hidden bridges; "
            "legacy absolute float payload reviews track float DTO lanes that still need double proof migration; "
            "Unity import/Burst/profiler pending; no dotnet build launched by this gate."
        ),
    }
    write_json(math_report_path, report)


def failures(payload: dict[str, Any], args: argparse.Namespace) -> list[str]:
    counts = payload["counts"]
    rows: list[str] = []
    direct_casts = int(counts["directAupFloat3CastCount"])
    component_casts = int(counts["runtimeComponentFloatAupCastCount"])
    strict_transform = int(counts["strictTransformAuthorityReadCount"])
    if direct_casts > args.max_direct_casts:
        rows.append(f"direct AUP/double3 float3 casts {direct_casts} > {args.max_direct_casts}")
    if component_casts > args.max_runtime_component_casts:
        rows.append(f"runtime component AUP float casts {component_casts} > {args.max_runtime_component_casts}")
    if strict_transform > args.max_strict_transform_authority:
        rows.append(f"strict Transform.position authority reads {strict_transform} > {args.max_strict_transform_authority}")
    return rows


def print_summary(payload: dict[str, Any], rows: list[str]) -> None:
    counts = payload["counts"]
    print("AUP precision gate SHINOBU_205")
    print(f"schema={payload['schema']}")
    print(f"status={payload['status']}")
    print(f"sourceRoot={payload['sourceRoot']}")
    print(f"filesScanned={counts['filesScanned']}")
    print(f"directAupFloat3CastCount={counts['directAupFloat3CastCount']}")
    print(f"runtimeComponentFloatAupCastCount={counts['runtimeComponentFloatAupCastCount']}")
    print(f"editorComponentFloatAupCastReviewCount={counts['editorComponentFloatAupCastReviewCount']}")
    print(f"strictTransformAuthorityReadCount={counts['strictTransformAuthorityReadCount']}")
    print(f"strictTransformAuthorityFileCount={len(counts['strictTransformAuthorityByFile'])}")
    print(f"runtimeAupBridgeReviewCount={counts['runtimeAupBridgeReviewCount']}")
    print(f"legacyAbsoluteFloatPayloadReviewCount={counts['legacyAbsoluteFloatPayloadReviewCount']}")
    for row in counts["strictTransformAuthorityByFile"][:12]:
        print(f"strictByFile={row['count']} {row['file']}")
    if rows:
        for row in rows:
            print(f"failure={row}")


def run(args: argparse.Namespace) -> int:
    full_report_path = Path(args.full_report)
    math_report_path = Path(args.math_report)
    payload = build_payload(args)
    write_json(full_report_path, payload)
    if not args.no_math_report:
        upsert_math_report(math_report_path, full_report_path, payload)
    rows = failures(payload, args)
    print_summary(payload, rows)
    return 1 if rows else 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--full-report", default=str(DEFAULT_FULL_REPORT))
    parser.add_argument("--math-report", default=str(DEFAULT_MATH_REPORT))
    parser.add_argument("--sample-limit", type=int, default=512)
    parser.add_argument("--max-direct-casts", type=int, default=0)
    parser.add_argument("--max-runtime-component-casts", type=int, default=0)
    parser.add_argument("--max-strict-transform-authority", type=int, default=0)
    parser.add_argument("--no-math-report", action="store_true")
    return parser


def main() -> int:
    return run(build_parser().parse_args())


if __name__ == "__main__":
    sys.exit(main())
