from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "Docs" / "Reports" / "LOGISTICS_RELEASE_JACOBI_AUDIT_X_010.json"
CORE_CSPROJ = ROOT / "Hecton8.Core.csproj"

X010_HOT_FILES = {
    "Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs",
    "Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs",
    "Assets/_Project/Scripts/Power/LogisticsGridTortureJob.cs",
    "Assets/_Project/Scripts/Construction/SumpPumpPipeGridContracts.cs",
    "Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs",
    "Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs",
    "Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs",
    "Assets/_Project/Scripts/Logistics/FluidPipePressureJobs.cs",
}

HEAVY_NUMERICAL_TOKENS = (
    "JacobiPowerGridSolverJob",
    "ApplyJacobiPowerRelaxation",
    "PowerGridRelaxationJob",
    "ResolvePropagationIterations",
    "ResolveSolverTargetTolerance",
    "ResolveSolverOmega",
    "JacobiTolerance",
    "JacobiResidual",
    "SolverOmega",
    "TargetTolerance",
)

LEGACY_NAME_TOKENS = (
    "PowerGridJacobiContracts",
    "PowerGridJacobiConstants",
    "jacobismoothingfactor",
)


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8-sig", errors="replace")


def compile_includes() -> list[Path]:
    text = read_text(CORE_CSPROJ)
    paths: list[Path] = []
    for match in re.finditer(r'<Compile Include="([^"]+)"', text):
        include = match.group(1).replace("\\", "/")
        path = ROOT / include
        if path.exists() and path.suffix.lower() == ".cs":
            paths.append(path)
    return paths


def main() -> int:
    findings: list[dict] = []
    x010_hot_failures: list[dict] = []
    release_files = compile_includes()
    for path in release_files:
        text = read_text(path)
        normalized = rel(path)
        for line_number, line in enumerate(text.splitlines(), start=1):
            tokens = [token for token in HEAVY_NUMERICAL_TOKENS if token in line]
            legacy_tokens = [token for token in LEGACY_NAME_TOKENS if token in line]
            if not tokens and not legacy_tokens:
                continue

            classification = "legacy_name"
            severity = "INFO"
            if tokens:
                classification = "heavy_numerical_method"
                severity = "WARN"
            if normalized in X010_HOT_FILES and tokens:
                classification = "x010_hot_forbidden"
                severity = "FAIL"

            finding = {
                "file": normalized,
                "line": line_number,
                "severity": severity,
                "classification": classification,
                "tokens": tokens + legacy_tokens,
                "text": line.strip(),
            }
            findings.append(finding)
            if severity == "FAIL":
                x010_hot_failures.append(finding)

    project_wide_heavy = [finding for finding in findings if finding["classification"] == "heavy_numerical_method"]
    legacy_names = [finding for finding in findings if finding["classification"] == "legacy_name"]
    report = {
        "agent": "X_010",
        "status": "PASS" if not x010_hot_failures else "FAIL",
        "projectWideZeroJacobiClaim": False if findings else True,
        "x010HotLogisticsHeavyJacobiCount": len(x010_hot_failures),
        "projectWideHeavyNumericalMethodCount": len(project_wide_heavy),
        "projectWideLegacyJacobiNameCount": len(legacy_names),
        "releaseCompileFileCount": len(release_files),
        "scope": "Hecton8.Core.csproj compile includes. PASS only means X_010 hot logistics files are clear; project-wide legacy/thermal Jacobi names are reported honestly.",
        "findings": findings,
    }
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
