#!/usr/bin/env python3
"""Run every offline hardware profile guard in one command.

This is a cold tooling entry point for integrators. It validates both the
generated runtime hardware catalog and the H8 system hardware profile bake,
including the deterministic H8 audit report.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ValidateHardwareProfileCatalog as runtime_catalog_guard  # noqa: E402
import ValidateSystemHardwareProfiles as system_profile_guard  # noqa: E402


def print_errors(prefix: str, errors: list[str]) -> None:
    for error in errors:
        print(f"{prefix}: {error}", file=sys.stderr)


def main() -> int:
    runtime_errors, runtime_stats = runtime_catalog_guard.validate()

    system_data = system_profile_guard.load_catalog()
    system_errors, system_report = system_profile_guard.validate_data(system_data)
    system_profile_guard.check_report(
        system_profile_guard.REPORT_PATH,
        system_report,
        system_errors,
    )

    if runtime_errors or system_errors:
        print("ALL_HARDWARE_PROFILE_GUARDS=FAIL", file=sys.stderr)
        print_errors("runtime_catalog", runtime_errors)
        print_errors("system_profile", system_errors)
        return 1

    summary = {
        "runtimeCatalogProfiles": runtime_stats["profiles"],
        "runtimeCatalogPhases": runtime_stats["phases"],
        "runtimeCatalogSplitJsons": runtime_stats["split_jsons"],
        "systemProfileCount": system_report["profileCount"],
        "systemQuest2TotalCommittedPlusReserveMb": system_report["quest2TotalCommittedPlusReserveMb"],
        "systemHotPathImpactMicroseconds": system_report["hotPathImpactMicroseconds"],
        "systemRuntimeGcImpactBytesPerFrame": system_report["runtimeGcImpactBytesPerFrame"],
    }
    print("ALL_HARDWARE_PROFILE_GUARDS=PASS " + json.dumps(summary, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
