#!/usr/bin/env python3
"""Run every offline hardware profile guard in one command.

This is a cold tooling entry point for integrators. It validates both the
generated runtime hardware catalog and the H8 system hardware profile bake,
including the deterministic H8 audit report.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
ROOT = SCRIPT_DIR.parents[1]
REPORT_PATH = ROOT / "Docs" / "AgentLogs" / "Hardware_Profile_All_Guards_H8_HARDWARE_TIER_MATRIX_BKR.json"
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ValidateHardwareProfileCatalog as runtime_catalog_guard  # noqa: E402
import ValidateSystemHardwareProfiles as system_profile_guard  # noqa: E402


def print_errors(prefix: str, errors: list[str]) -> None:
    for error in errors:
        print(f"{prefix}: {error}", file=sys.stderr)


def relative_path(path: Path) -> str:
    resolved = path.resolve()
    try:
        return resolved.relative_to(ROOT).as_posix()
    except ValueError:
        return resolved.as_posix()


def run_guards() -> tuple[list[str], dict[str, object]]:
    runtime_errors, runtime_stats = runtime_catalog_guard.validate()

    system_data = system_profile_guard.load_catalog()
    system_errors, system_report = system_profile_guard.validate_data(system_data)
    system_profile_guard.check_report(
        system_profile_guard.REPORT_PATH,
        system_report,
        system_errors,
    )

    errors: list[str] = []
    errors.extend(f"runtime_catalog: {error}" for error in runtime_errors)
    errors.extend(f"system_profile: {error}" for error in system_errors)
    report: dict[str, object] = {
        "status": "PASS" if not errors else "FAIL",
        "ownerPromptId": "H8_HARDWARE_TIER_MATRIX_BKR",
        "runtimeCatalog": {
            "profiles": runtime_stats.get("profiles", 0),
            "phases": runtime_stats.get("phases", 0),
            "masks": runtime_stats.get("masks", 0),
            "constants": runtime_stats.get("constants", 0),
            "splitJsons": runtime_stats.get("split_jsons", 0),
        },
        "systemProfile": {
            "profileCount": system_report.get("profileCount", 0),
            "profileIds": system_report.get("profileIds", []),
            "quest2TotalCommittedPlusReserveMb": system_report.get("quest2TotalCommittedPlusReserveMb", 0),
            "stressWeightSum": system_report.get("stressWeightSum", 0.0),
            "hotPathImpactMicroseconds": system_report.get("hotPathImpactMicroseconds", 0),
            "runtimeGcImpactBytesPerFrame": system_report.get("runtimeGcImpactBytesPerFrame", 0),
        },
        "systemAuditReportPath": relative_path(system_profile_guard.REPORT_PATH),
        "compileStatus": "NOT_COMPILED_STATIC_GUARDS_ONLY",
        "unityRuntimeStatus": "NOT_VERIFIED_STATIC_TOOL_ONLY",
        "errors": errors,
    }
    return errors, report


def write_report(path: Path, report: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


def check_report(path: Path, report: dict[str, object], errors: list[str]) -> None:
    if not path.exists():
        errors.append(f"missing aggregate report: {relative_path(path)}")
        return
    with path.open("r", encoding="utf-8-sig") as handle:
        stored = json.load(handle)
    if stored != report:
        errors.append(f"aggregate report drift: {relative_path(path)}")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run all offline hardware profile guards")
    parser.add_argument("--report", type=Path, default=REPORT_PATH, help="Aggregate audit report path")
    parser.add_argument("--write-report", action="store_true", help="Write deterministic aggregate report")
    parser.add_argument("--check-report", action="store_true", help="Require aggregate report to match current guard output")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    errors, report = run_guards()

    if args.write_report:
        write_report(args.report, report)
    if args.check_report:
        check_report(args.report, report, errors)

    if errors:
        print("ALL_HARDWARE_PROFILE_GUARDS=FAIL", file=sys.stderr)
        print_errors("guard", errors)
        return 1

    summary = {
        "runtimeCatalogProfiles": report["runtimeCatalog"]["profiles"],
        "runtimeCatalogPhases": report["runtimeCatalog"]["phases"],
        "runtimeCatalogSplitJsons": report["runtimeCatalog"]["splitJsons"],
        "systemProfileCount": report["systemProfile"]["profileCount"],
        "systemQuest2TotalCommittedPlusReserveMb": report["systemProfile"]["quest2TotalCommittedPlusReserveMb"],
        "systemHotPathImpactMicroseconds": report["systemProfile"]["hotPathImpactMicroseconds"],
        "systemRuntimeGcImpactBytesPerFrame": report["systemProfile"]["runtimeGcImpactBytesPerFrame"],
    }
    print("ALL_HARDWARE_PROFILE_GUARDS=PASS " + json.dumps(summary, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
