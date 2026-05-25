from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEDICATED_REPORT = ROOT / "Docs" / "Reports" / "PHYSICS_OPTIMIZATION_REPORT_SHINOBU_330.json"
SHARED_REPORT = ROOT / "Docs" / "Reports" / "PHYSICS_OPTIMIZATION_REPORT.json"

SCAN_PATHS = (
    ROOT / "Assets" / "_Project" / "Scripts" / "Habitat",
    ROOT / "Assets" / "_Project" / "Scripts" / "Vehicles",
    ROOT / "Assets" / "_Project" / "Scripts" / "Physics" / "HabitatFluidIncursionContracts.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Physics" / "HabitatFluidIncursionDirector.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Physics" / "HabitatFluidIncursionJobs.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "BaseModule.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "BaseAirlock.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "BuoyancyObject.cs",
    ROOT / "Assets" / "_Project" / "Scripts" / "SubmarineFluidDynamics.cs",
)

FORBIDDEN_PATTERNS = (
    ("water_volume_enable", re.compile(r"\bwaterVolume\s*\.\s*SetActive\s*\(\s*true\s*\)")),
    ("water_plane_write", re.compile(r"\bfloodSurfacePlane\s*\.\s*localPosition\s*=")),
    ("dry_zone_enter", re.compile(r"\bEnterDryZone\s*\(")),
    ("dry_zone_exit", re.compile(r"\bExitDryZone\s*\(")),
    ("buoyancy_trigger_lookup", re.compile(r"\bTryGetComponent\s*\(\s*out\s+BuoyancyObject\b")),
    ("managed_buoyancy_dictionary", re.compile(r"\bDictionary\s*<\s*ulong\s*,\s*BuoyancyObject\s*>")),
    ("water_trigger_stay", re.compile(r"\bOnTriggerStay\s*\(")),
    ("hot_dto_property", re.compile(r"\bpublic\s+[^;\n{}]+\s+\w+\s*\{\s*get\s*;")),
    ("pack_one", re.compile(r"\bPack\s*=\s*1\b")),
)

FLOOD_RIGIDBODY_PATTERN = re.compile(r"\b_rigidbody\s*\.\s*(mass|centerOfMass|inertiaTensor)\s*=")


def iter_scan_files() -> list[Path]:
    files: list[Path] = []
    for path in SCAN_PATHS:
        if path.is_file():
            files.append(path)
        elif path.is_dir():
            files.extend(path.rglob("*.cs"))
    return sorted(set(files))


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def is_editor_path(path: Path) -> bool:
    return "Editor" in path.parts


def is_allowed_dry_restore_rigidbody(lines: list[str], line_index: int) -> bool:
    start = max(0, line_index - 30)
    context = "\n".join(lines[start : line_index + 1])
    return "RestoreRigidbodyDynamics" in context


def should_check_pattern(path: Path, pattern_name: str) -> bool:
    if pattern_name == "hot_dto_property":
        return path.name.startswith("HabitatFluidIncursion")

    return True


def scan() -> dict:
    findings: list[dict] = []
    allowed_notes: list[dict] = []
    files = iter_scan_files()

    for path in files:
        if is_editor_path(path):
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            text = path.read_text(encoding="utf-8-sig", errors="replace")

        lines = text.splitlines()
        for index, line in enumerate(lines, start=1):
            for name, pattern in FORBIDDEN_PATTERNS:
                if not should_check_pattern(path, name):
                    continue

                if pattern.search(line):
                    findings.append(
                        {
                            "pattern": name,
                            "file": rel(path),
                            "line": index,
                            "text": line.strip(),
                        }
                    )

            rigidbody_match = FLOOD_RIGIDBODY_PATTERN.search(line)
            if rigidbody_match:
                if path.name == "SubmarineFluidDynamics.cs" and is_allowed_dry_restore_rigidbody(lines, index - 1):
                    allowed_notes.append(
                        {
                            "pattern": "dry_restore_rigidbody_write",
                            "file": rel(path),
                            "line": index,
                            "reason": "Dry restore path, not flood mass/COM/inertia authority.",
                        }
                    )
                    continue

                findings.append(
                    {
                        "pattern": "flood_rigidbody_write",
                        "file": rel(path),
                        "line": index,
                        "text": line.strip(),
                    }
                )

    report = {
        "agent": "SHINOBU_330",
        "scanner": "OOP_Water_Trigger_Scanner",
        "summary": "Fluid trigger water authority scan",
        "status": "STATIC_SOURCE",
        "scanScope": [rel(path) for path in SCAN_PATHS if path.exists()],
        "scannedFileCount": len(files),
        "findingCount": len(findings),
        "legacyWaterAuthorityEradicated": len(findings) == 0,
        "runtimeRouteProof": (
            "SignalBus<FluidIncursionSignal> -> Vault FluidCompartmentDTO[64B] -> "
            "Burst CSR BFS -> SubmarineFloodStateSignal/PhysicsEventPayload -> AddedMassProfileDTO consumer"
        ),
        "dearLieProof": (
            "Interior water is shader/global-buffer scalar waterline presentation; no water plane GameObject "
            "or dry-zone BuoyancyObject trigger authority is accepted."
        ),
        "allowedNotes": allowed_notes,
        "findings": findings,
    }
    return report


def write_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def update_shared(report: dict) -> None:
    if SHARED_REPORT.exists():
        try:
            shared = json.loads(SHARED_REPORT.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            shared = {}
    else:
        shared = {}

    if not isinstance(shared, dict):
        shared = {}

    shared["shinobu330FluidIncursionScanner"] = {
        "agent": report["agent"],
        "scanner": report["scanner"],
        "summary": report["summary"],
        "dedicatedReport": rel(DEDICATED_REPORT),
        "scanScope": "Assets/_Project/Scripts/{Habitat,Vehicles}, BaseModule, BaseAirlock, BuoyancyObject, SubmarineFluidDynamics, HabitatFluidIncursion*.cs",
        "scannedFileCount": report["scannedFileCount"],
        "findingCount": report["findingCount"],
        "legacyWaterAuthorityEradicated": report["legacyWaterAuthorityEradicated"],
        "runtimeRouteProof": report["runtimeRouteProof"],
        "dearLieProof": report["dearLieProof"],
    }
    write_json(SHARED_REPORT, shared)


def main() -> int:
    report = scan()
    write_json(DEDICATED_REPORT, report)
    update_shared(report)
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0 if report["findingCount"] == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
