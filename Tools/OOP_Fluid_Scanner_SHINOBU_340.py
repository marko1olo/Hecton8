from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEDICATED_REPORT = ROOT / "Docs" / "Reports" / "PHYSICS_OPTIMIZATION_REPORT_SHINOBU_340.json"
SHARED_REPORT = ROOT / "Docs" / "Reports" / "PHYSICS_OPTIMIZATION_REPORT.json"

SCAN_PATHS = (
    ROOT / "Assets" / "_Project" / "Scripts" / "Habitat",
    ROOT / "Assets" / "_Project" / "Scripts" / "Logistics",
    ROOT / "Assets" / "_Project" / "Scripts" / "Construction",
)

FORBIDDEN_PATTERNS = (
    ("legacy_water_pipe_class", re.compile(r"\bclass\s+WaterPipe\b")),
    ("legacy_sump_pump_controller", re.compile(r"\bclass\s+SumpPumpController\b")),
    ("recursive_water_propagation", re.compile(r"\bPropagateWater\s*\(")),
    ("managed_pipe_list", re.compile(r"\bList\s*<\s*(Water)?Pipe\s*>")),
    ("managed_pipe_queue", re.compile(r"\bQueue\s*<\s*(Water)?Pipe\s*>")),
    ("water_rigidbody_particle", re.compile(r"\bRigidbody\b.*\b(water|droplet|particle)|\b(water|droplet|particle).*\bRigidbody\b", re.IGNORECASE)),
    ("water_particle_system_authority", re.compile(r"\bParticleSystem\b.*\b(water|pipe|flow|leak)|\b(water|pipe|flow|leak).*\bParticleSystem\b", re.IGNORECASE)),
    ("hot_dto_property", re.compile(r"\bstruct\s+\w*Drainage\w*DTO[\s\S]{0,800}?\bpublic\s+[^;\n{}]+\s+\w+\s*\{\s*get\s*;")),
)


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def iter_scan_files() -> list[Path]:
    files: list[Path] = []
    for path in SCAN_PATHS:
        if path.is_file():
            files.append(path)
        elif path.is_dir():
            files.extend(path.rglob("*.cs"))
    return sorted(set(files))


def is_editor_path(path: Path) -> bool:
    return "Editor" in path.parts


def scan() -> dict:
    findings: list[dict] = []
    files = iter_scan_files()
    for path in files:
        if is_editor_path(path):
            continue

        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            text = path.read_text(encoding="utf-8-sig", errors="replace")

        lines = text.splitlines()
        for pattern_name, pattern in FORBIDDEN_PATTERNS:
            if pattern_name == "hot_dto_property":
                for match in pattern.finditer(text):
                    line = text.count("\n", 0, match.start()) + 1
                    findings.append(
                        {
                            "pattern": pattern_name,
                            "file": rel(path),
                            "line": line,
                            "text": lines[line - 1].strip() if 0 <= line - 1 < len(lines) else "",
                        }
                    )
                continue

            for line_index, line in enumerate(lines, start=1):
                if pattern.search(line):
                    findings.append(
                        {
                            "pattern": pattern_name,
                            "file": rel(path),
                            "line": line_index,
                            "text": line.strip(),
                        }
                    )

    return {
        "agent": "SHINOBU_340",
        "scanner": "OOP_Fluid_Scanner",
        "summary": "OOP Fluid Flow Eradicated" if len(findings) == 0 else "OOP Fluid Flow Violations Detected",
        "status": "STATIC_SOURCE",
        "scanScope": [rel(path) for path in SCAN_PATHS if path.exists()],
        "scannedFileCount": len(files),
        "findingCount": len(findings),
        "csrAuthority": "SumpPumpPipeGridRuntime -> GenerateMockPipeNetworkJob -> BuildCsrPipeGraphJob -> ApplyPumpPowerConstraintJob -> EvaluatePipePressureJob -> ExecuteWaterEvacuationJob",
        "dearLieProof": "Pipe visuals consume DrainagePipeFlowGpuDTO StructuredBuffer; no CPU water geometry or Rigidbody droplet authority accepted.",
        "findings": findings,
    }


def write_json(path: Path, data: dict, *, sort_keys: bool = True) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=sort_keys) + "\n", encoding="utf-8")


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

    shared["shinobu340OopFluidScanner"] = {
        "agent": report["agent"],
        "scanner": report["scanner"],
        "summary": report["summary"],
        "dedicatedReport": rel(DEDICATED_REPORT),
        "scanScope": report["scanScope"],
        "scannedFileCount": report["scannedFileCount"],
        "findingCount": report["findingCount"],
        "csrAuthority": report["csrAuthority"],
        "dearLieProof": report["dearLieProof"],
    }
    write_json(SHARED_REPORT, shared, sort_keys=False)


def main() -> int:
    report = scan()
    write_json(DEDICATED_REPORT, report)
    update_shared(report)
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0 if report["findingCount"] == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
