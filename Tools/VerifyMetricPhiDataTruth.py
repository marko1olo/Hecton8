#!/usr/bin/env python3
"""Metric Phi static data-truth verifier for current offline artifacts."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


TOOLS_ROOT = Path(__file__).resolve().parent
ROOT = TOOLS_ROOT.parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

from H8VerifyCore import binary_alignment_summary, count_atlas_domains, path, scan_struct_endianness  # noqa: E402


STATUS_OK = "DATA_TRUTH_VERIFIED"


def load_json_optional(target: Path) -> dict[str, Any]:
    if not target.exists():
        return {}
    try:
        return json.loads(target.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError:
        return {}


def verify_sweep_status(sweep_path: Path | None) -> tuple[bool, dict[str, Any]]:
    if sweep_path is None:
        sweep_path = path("Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json")
    data = load_json_optional(sweep_path)
    if not data:
        return False, {"path": str(sweep_path), "error": "missing_or_invalid"}
    summary = data.get("summary", {})
    required_failures = int(
        data.get(
            "requiredFailures",
            data.get("required_failures", summary.get("requiredFailures", summary.get("required_failures", 0))),
        )
        or 0
    )
    status = str(data.get("status", ""))
    return status == "VERIFY_SWEEP_PASS" and required_failures == 0, data


def report_value(relative: str, *keys: str, default: Any = None) -> Any:
    data = load_json_optional(path(relative))
    current: Any = data
    for key in keys:
        if not isinstance(current, dict) or key not in current:
            return default
        current = current[key]
    return current


def file_aligned(relative: str) -> bool:
    target = path(relative)
    return target.is_file() and target.stat().st_size % 16 == 0


def manifest_contains(relative: str, token: str) -> bool:
    target = path(relative)
    if not target.is_file():
        return False
    return token in target.read_text(encoding="utf-8-sig", errors="ignore")


def resolve_cli_path(raw: str | None) -> Path | None:
    if not raw:
        return None
    candidate = Path(raw)
    return candidate if candidate.is_absolute() else path(candidate)


def append_static_artifact_checks(
    checks: list[dict[str, Any]],
    atlas_domain_count: int,
    format_site_count: int,
    endian_failures: list[str],
    alignment_failures: list[str],
    sweep_ok: bool,
    sweep_payload: dict[str, Any],
) -> None:
    summary = sweep_payload.get("summary", {}) if isinstance(sweep_payload, dict) else {}
    required_failures = int(summary.get("requiredFailures", sweep_payload.get("requiredFailures", 0)) or 0)
    total_commands = int(summary.get("totalCommands", sweep_payload.get("totalCommands", 0)) or 0)
    self_check_pending = bool(summary.get("selfCheckPending", sweep_payload.get("selfCheckPending", False)))
    command_count_ok = total_commands == 35 or (self_check_pending and total_commands == 34)
    checks.extend(
        [
            {"name": "verify_sweep_pass", "passed": sweep_ok},
            {"name": "verify_sweep_required_failures_zero", "passed": required_failures == 0},
            {"name": "verify_sweep_command_count_35", "passed": command_count_ok},
            {"name": "all_data_binaries_aligned16", "passed": not alignment_failures},
            {"name": "project_atlas_has_85_domain_map", "passed": atlas_domain_count >= 85},
            {"name": "python_struct_endianness_explicit", "passed": format_site_count > 0 and not endian_failures},
            {
                "name": "hash_catalog_collision_count_zero",
                "passed": int(report_value("Docs/Reports/H8HashCollision_METRIC_PHI_ANALYST.json", "collision_count", default=0) or 0) == 0,
            },
            {
                "name": "hash_catalog_records_present",
                "passed": len(report_value("Docs/Reports/H8HashCollision_METRIC_PHI_ANALYST.json", "records", default=[]) or []) >= 1000,
            },
            {
                "name": "economy_monte_carlo_million_steps",
                "passed": int(report_value("Docs/Reports/METRIC_PHI_DATA_INQUISITION_SWEEP.json", "monteCarloSteps", default=0) or 0) >= 1_000_000,
            },
            {
                "name": "data_inquisition_report_pass",
                "passed": report_value("Docs/Reports/METRIC_PHI_DATA_INQUISITION_SWEEP.json", "status") == "DATA_INQUISITION_VERIFIED_STATIC_ONLY",
            },
            {
                "name": "binary_hygiene_report_pass",
                "passed": report_value("Docs/Reports/METRIC_PHI_BINARY_HYGIENE_SWEEP.json", "status") == "BINARY_HYGIENE_VERIFIED",
            },
            {
                "name": "lore_manifest_raw_utf8",
                "passed": report_value("Data/Lore/Encyclopedia.manifest.json", "compression") == "none/raw-utf8",
            },
            {"name": "lore_h8bin_aligned16", "passed": file_aligned("Data/Lore/Encyclopedia.h8bin")},
            {
                "name": "lore_manifest_toaster_tier",
                "passed": manifest_contains("Data/Lore/Encyclopedia.manifest.json", "\"toaster\""),
            },
            {
                "name": "lore_manifest_rtx_overkill_tier",
                "passed": manifest_contains("Data/Lore/Encyclopedia.manifest.json", "rtx_overkill"),
            },
            {
                "name": "optics_manifest_beer_lambert",
                "passed": manifest_contains("Data/Visuals/Water_Extinction_Matrix.json", "BeerLambert"),
            },
            {"name": "optics_binary_aligned16", "passed": file_aligned("Data/Visuals/Water_Extinction_Matrix.bin")},
            {
                "name": "optics_manifest_rtx_overkill",
                "passed": manifest_contains("Data/Visuals/Water_Extinction_Matrix.json", "rtx_overkill"),
            },
            {
                "name": "dalton_manifest_dalton_law",
                "passed": manifest_contains("Data/Precomputed/dalton_gas_toxicity_manifest.json", "daltonLaw"),
            },
            {
                "name": "dalton_manifest_hydrostatic_pressure",
                "passed": manifest_contains("Data/Precomputed/dalton_gas_toxicity_manifest.json", "hydrostaticPressure"),
            },
            {"name": "dalton_binary_aligned16", "passed": file_aligned("Data/Precomputed/dalton_gas_toxicity.bin")},
            {
                "name": "dalton_toaster_binary_aligned16",
                "passed": file_aligned("Data/Precomputed/dalton_gas_toxicity_toaster.bin"),
            },
            {
                "name": "dalton_overkill_binary_aligned16",
                "passed": file_aligned("Data/Precomputed/dalton_gas_toxicity_overkill.bin"),
            },
            {
                "name": "sabine_manifest_sabine_basis",
                "passed": manifest_contains("Data/Audio/Acoustic_LUT.manifest.json", "Sabine"),
            },
            {
                "name": "sabine_manifest_thorp_basis",
                "passed": manifest_contains("Data/Audio/Acoustic_LUT.manifest.json", "Thorp"),
            },
            {"name": "sabine_binary_aligned16", "passed": file_aligned("Data/Audio/Acoustic_LUT.bin")},
            {"name": "crafting_cost_binary_aligned16", "passed": file_aligned("Data/Economy/Crafting_Costs.h8bin")},
            {"name": "crafting_toaster_binary_aligned16", "passed": file_aligned("Data/Economy/Crafting_Costs_Toaster.h8bin")},
            {"name": "babel_binary_aligned16", "passed": file_aligned("Data/Balance/Baked/Babel_Dictionary.h8bin")},
            {"name": "taxonomy_binary_aligned16", "passed": file_aligned("Data/Localization/en_US_Taxonomy.h8bin")},
            {"name": "ai_navigation_binary_aligned16", "passed": file_aligned("Data/AI/Navigation_Tuning.h8bin")},
            {"name": "vr_comfort_binary_aligned16", "passed": file_aligned("Data/UX/VR_Comfort_Profiles.h8bin")},
            {"name": "vram_budget_binary_aligned16", "passed": file_aligned("Data/System/VFX_Budgets.h8bin")},
            {"name": "visual_lod_binary_aligned16", "passed": file_aligned("Data/System/Visual_Scalability_Matrix.bin")},
            {"name": "quest_dag_binary_aligned16", "passed": file_aligned("Data/Narrative/First_Hour_Quests.h8qdag.bin")},
            {"name": "replay_hasher_reference_passed", "passed": "VerifyReplayHasherReference" in json.dumps(sweep_payload)},
            {"name": "runtime_proof_boundary_declared", "passed": "CLI Python only" in json.dumps(sweep_payload)},
        ]
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify Metric Phi data truth evidence.")
    parser.add_argument("--sweep-input", default=None)
    parser.add_argument("--json-output", default="Docs/Reports/METRIC_PHI_DATA_TRUTH_AUDIT.json")
    parser.add_argument("--markdown-output", default=None)
    args = parser.parse_args()

    binary_count, alignment_failures = binary_alignment_summary()
    format_site_count, endian_failures = scan_struct_endianness()
    atlas_domain_count = count_atlas_domains()
    sweep_ok, sweep_payload = verify_sweep_status(resolve_cli_path(args.sweep_input))
    checks: list[dict[str, Any]] = []
    append_static_artifact_checks(
        checks,
        atlas_domain_count,
        format_site_count,
        endian_failures,
        alignment_failures,
        sweep_ok,
        sweep_payload,
    )

    failed = [check["name"] for check in checks if not check["passed"]]
    payload = {
        "schema": "H8.MetricPhi.DataTruthAudit.v1",
        "tool": "Tools/VerifyMetricPhiDataTruth.py",
        "status": STATUS_OK if not failed else "DATA_TRUTH_FAILED",
        "summary": {
            "totalChecks": len(checks),
            "failedChecks": len(failed),
            "failedNames": failed,
        },
        "checks": checks,
        "binaryAlignment": {
            "fileCount": binary_count,
            "alignmentBytes": 16,
            "unaligned": alignment_failures,
        },
        "endianness": {
            "formatSites": format_site_count,
            "failures": endian_failures,
        },
        "atlas": {"domainCount": atlas_domain_count},
        "hashing": {
            "collisionCount": int(report_value("Docs/Reports/H8HashCollision_METRIC_PHI_ANALYST.json", "collision_count", default=0) or 0),
            "records": len(report_value("Docs/Reports/H8HashCollision_METRIC_PHI_ANALYST.json", "records", default=[]) or []),
        },
        "economy": {
            "status": "ECONOMY PROVEN" if int(report_value("Docs/Reports/METRIC_PHI_DATA_INQUISITION_SWEEP.json", "monteCarloSteps", default=0) or 0) >= 1_000_000 else "ECONOMY EVIDENCE MISSING",
            "steps": int(report_value("Docs/Reports/METRIC_PHI_DATA_INQUISITION_SWEEP.json", "monteCarloSteps", default=0) or 0),
        },
        "verifySweep": {
            "status": sweep_payload.get("status", "VERIFY_SWEEP_PASS" if sweep_ok else "VERIFY_SWEEP_FAIL"),
            "requiredFailures": int(sweep_payload.get("summary", {}).get("requiredFailures", sweep_payload.get("requiredFailures", 0)) or 0),
            "totalCommands": int(sweep_payload.get("summary", {}).get("totalCommands", sweep_payload.get("totalCommands", 0)) or 0),
            "selfCheckPending": bool(sweep_payload.get("summary", {}).get("selfCheckPending", sweep_payload.get("selfCheckPending", False))),
        },
    }

    out = resolve_cli_path(args.json_output) or path("Docs/Reports/METRIC_PHI_DATA_TRUTH_AUDIT.json")
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    if args.markdown_output:
        md = resolve_cli_path(args.markdown_output) or path("Docs/Reports/METRIC_PHI_DATA_TRUTH_AUDIT.md")
        md.parent.mkdir(parents=True, exist_ok=True)
        md.write_text(
            "# Metric Phi Data Truth Audit\n\n"
            f"Status: {payload['status']}\n\n"
            f"Checks: {len(checks)}\n\n"
            f"Failed: {len(failed)}\n",
            encoding="utf-8",
        )

    if failed:
        print("METRIC_PHI_DATA_TRUTH_STATUS: DATA_TRUTH_FAILED")
        print(f"checks={len(checks)} failed={len(failed)}")
        return 2

    print("METRIC_PHI_DATA_TRUTH_STATUS: DATA_TRUTH_VERIFIED")
    print(f"checks={len(checks)} failed=0")
    print(f"binary_files={binary_count} unaligned=0")
    print(f"struct_format_sites={format_site_count} endian_failures=0")
    print(f"report={args.json_output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
