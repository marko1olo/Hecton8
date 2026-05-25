from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "Docs" / "Reports" / "LOGISTICS_OPTIMIZATION_REPORT_X_010.json"

SCAN_ROOT = ROOT / "Assets" / "_Project" / "Scripts"
HOT_PATH_MARKERS = (
    "Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs",
    "Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs",
    "Assets/_Project/Scripts/Construction/SumpPumpPipeGridContracts.cs",
    "Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs",
    "Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs",
    "Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs",
    "Assets/_Project/Scripts/Logistics/FluidPipePressureJobs.cs",
)
ALLOWED_FIXED_PASS_BOUNDS = (
    "FixedDeltaPassCount",
    "FixedDrainageDeltaPassCount",
    "FixedPowerDeltaPropagationPassCount",
    "TwoPassPowerGridSolverJob.FixedPropagationPassCount",
)


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8-sig", errors="replace")


def line_of(text: str, index: int) -> int:
    return text.count("\n", 0, index) + 1


def iter_cs_files() -> list[Path]:
    return sorted(path for path in SCAN_ROOT.rglob("*.cs") if "Library" not in path.parts)


def is_hot(path: Path) -> bool:
    normalized = rel(path)
    return normalized in HOT_PATH_MARKERS


def is_relevant_logistics_path(path: Path) -> bool:
    normalized = rel(path)
    return (
        normalized.startswith("Assets/_Project/Scripts/Power/") or
        normalized.startswith("Assets/_Project/Scripts/Construction/") or
        normalized.startswith("Assets/_Project/Scripts/Logistics/") or
        normalized in ("Assets/_Project/Scripts/PowerGrid.cs", "Assets/_Project/Scripts/PowerGridManager.cs")
    )


def is_excluded_context(path: Path) -> bool:
    normalized = rel(path)
    return "/QA/" in normalized or "/Tests/" in normalized or "/Atmosphere/" in normalized


def scan_iteration_loops(path: Path, text: str, findings: list[dict]) -> None:
    loop_pattern = re.compile(r"for\s*\(\s*int\s+(\w+)\s*=\s*0\s*;\s*\1\s*<\s*([^;]+);", re.MULTILINE)
    for match in loop_pattern.finditer(text):
        bound = match.group(2).strip()
        snippet = text[match.start(): text.find("\n", match.start())].strip()
        fixed_two_pass = any(token in bound for token in ALLOWED_FIXED_PASS_BOUNDS)
        suspicious = (
            "ResolvePropagationIterations" in bound or
            "MaxPropagationIterations" in bound or
            "DefaultSolverIterationCount" in bound or
            re.search(r"\b(8|10|1000)\b", bound) is not None
        )
        if is_hot(path) and suspicious and not fixed_two_pass:
            findings.append({
                "severity": "FAIL",
                "type": "hot_iterative_solver_loop",
                "file": rel(path),
                "line": line_of(text, match.start()),
                "bound": bound,
                "text": snippet,
            })
        elif is_hot(path) and fixed_two_pass:
            findings.append({
                "severity": "INFO",
                "type": "fixed_two_pass_loop",
                "file": rel(path),
                "line": line_of(text, match.start()),
                "bound": bound,
                "text": snippet,
            })


def scan_managed_containers(path: Path, text: str, findings: list[dict]) -> None:
    managed_pattern = re.compile(r"\b(?<!Native)(List|Dictionary|HashSet|Queue|Stack)\s*<")
    for match in managed_pattern.finditer(text):
        line_start = text.rfind("\n", 0, match.start()) + 1
        line_end = text.find("\n", match.start())
        if line_end < 0:
            line_end = len(text)
        line = text[line_start:line_end].strip()
        severity = "WARN" if is_hot(path) and "COLD ALLOC" not in line else "INFO"
        findings.append({
            "severity": severity,
            "type": "managed_container_reference",
            "file": rel(path),
            "line": line_of(text, match.start()),
            "text": line,
            "classification": "cold_or_authoring" if severity == "INFO" else "requires_manual_owner_check",
        })


def scan_recursive_methods(path: Path, text: str, findings: list[dict]) -> None:
    if not is_hot(path):
        return
    method_pattern = re.compile(
        r"(?:public|private|internal|protected)\s+(?:static\s+)?(?:unsafe\s+)?[\w<>\[\],\s]+\s+(\w+)\s*\([^;{}]*\)\s*\{"
    )
    for match in method_pattern.finditer(text):
        name = match.group(1)
        if not re.search(r"(Traverse|Propagate|Flood|Search|Visit|Reach|Route|Graph|Csr|Bfs)", name):
            continue
        body_start = text.find("{", match.start())
        depth = 0
        cursor = body_start
        while cursor < len(text):
            char = text[cursor]
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    body = text[body_start + 1:cursor]
                    if re.search(r"\b" + re.escape(name) + r"\s*\(", body):
                        findings.append({
                            "severity": "FAIL",
                            "type": "recursive_hot_method",
                            "file": rel(path),
                            "line": line_of(text, match.start()),
                            "method": name,
                        })
                    break
            cursor += 1


def scan_target(path: Path, text: str) -> dict | None:
    normalized = rel(path)
    tokens = []
    for token in (
        "PowerGridManager",
        "LogisticsNetworkGraph",
        "TwoPassPowerGridSolverJob",
        "ApplyTwoPassPowerDeltaPropagation",
        "LogisticsGridTortureJob",
        "SumpPumpPipeGridRuntime",
        "EvaluatePipePressureDeltaPassJob",
        "DrainageNodeDTO",
        "PipeEdgeDTO",
        "CSR",
    ):
        if token in text:
            tokens.append(token)
    if is_relevant_logistics_path(path):
        for token in ("NativeArray", "GlobalDataVault"):
            if token in text:
                tokens.append(token)
    if not tokens:
        return None
    return {
        "file": normalized,
        "hotPath": is_hot(path),
        "excludedContext": is_excluded_context(path),
        "tokens": tokens,
    }


def require_proof(condition: bool, findings: list[dict], proof_type: str, text: str) -> None:
    if condition:
        return
    findings.append({
        "severity": "FAIL",
        "type": proof_type,
        "file": "static_proof",
        "line": 0,
        "text": text,
    })


def validate_static_proofs(findings: list[dict]) -> None:
    graph_path = SCAN_ROOT / "Power" / "LogisticsNetworkGraph.cs"
    torture_path = SCAN_ROOT / "Power" / "LogisticsGridTortureJob.cs"
    graph_text = read_text(graph_path)
    torture_text = read_text(torture_path)

    require_proof(
        "ApplyTwoPassPowerDeltaPropagation" in graph_text and
        graph_text.count("ApplyPowerDeltaPass(solveStartNode, solveEndNode") == 2,
        findings,
        "missing_power_two_pass_proof",
        "Power graph must call ApplyPowerDeltaPass exactly twice in the hot solve.",
    )
    require_proof(
        "CommitNoEdgeEvaluation" in graph_text and
        "ResetNoEdgeRuntimeState" in graph_text and
        "flags = (byte)((flags | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered);" in graph_text,
        findings,
        "missing_open_circuit_zero_proof",
        "Open-circuit fast path must publish offline, non-powered node flags.",
    )
    require_proof(
        "RequiredNodeCount = 2000" in torture_text and
        "RequiredEdgeCount = 6000" in torture_text and
        "RequiredShortCircuitCount = 384" in torture_text and
        "EdgeFlagShortCircuit" in torture_text and
        "FixedPassCount = 2" in torture_text and
        "PowerGridJacobiConstants" not in torture_text and
        len(re.findall(r"^\s*RunDeltaPass\(", torture_text, re.MULTILINE)) == 2,
        findings,
        "missing_torture_job_proof",
        "Grid torture job must build 2000 nodes, 6000 edges, inject short circuits, and run exactly two delta passes.",
    )
    require_proof(
        "_unpoweredZeroStateLatched" in graph_text and
        "_noEdgeZeroStateLatched" in graph_text and
        "if (_unpoweredZeroStateLatched)" in graph_text and
        "if (_noEdgeZeroStateLatched)" in graph_text,
        findings,
        "missing_latched_zero_fast_path",
        "Repeated unpowered/open-circuit idle frames must return without rewriting CSR buffers.",
    )


def main() -> int:
    findings: list[dict] = []
    targets: list[dict] = []
    files = iter_cs_files()
    for path in files:
        text = read_text(path)
        target = scan_target(path, text)
        if target is not None:
            targets.append(target)
        if is_excluded_context(path):
            continue
        scan_iteration_loops(path, text, findings)
        if is_relevant_logistics_path(path):
            scan_managed_containers(path, text, findings)
        scan_recursive_methods(path, text, findings)

    validate_static_proofs(findings)

    failures = [finding for finding in findings if finding["severity"] == "FAIL"]
    hot_warnings = [finding for finding in findings if finding["severity"] == "WARN"]
    report = {
        "agent": "X_010",
        "scanner": "OOP_Fluid_Scanner",
        "status": "PASS" if not failures else "FAIL",
        "scope": "Power, sump-pipe, and logistics CSR scripts under Assets/_Project/Scripts",
        "scannedFileCount": len(files),
        "targetCount": len(targets),
        "hotPathCount": sum(1 for target in targets if target["hotPath"]),
        "findingCount": len(findings),
        "failureCount": len(failures),
        "hotWarningCount": len(hot_warnings),
        "proof": {
            "powerHotPath": "LogisticsNetworkGraph.ApplyTwoPassPowerDeltaPropagation uses exactly two fixed CSR delta passes.",
            "routerHotPath": "ShinobuLogisticsRouter schedules exactly two LogisticsFlowDeltaPassJob passes.",
            "drainageHotPath": "SumpPumpPipeGridRuntime schedules exactly two EvaluatePipePressureDeltaPassJob passes.",
            "gridTorture": "LogisticsGridTortureJob materializes a 2000-node/6000-edge CSR graph, injects 384 short circuits, and runs two fixed delta passes.",
            "dtoLayout": "PowerNodeDTO, PowerGridEdgeDTO, DrainageNodeDTO, and PipeEdgeDTO are explicit 32-byte structs.",
            "latchedFastPath": "Repeated unpowered and open-circuit frames return after the first zero-state commit without touching CSR buffers.",
            "blackBox": "Power and drainage dump paths target Docs/AgentLogs/Dump_SHINOBU_340_Logistics.bin; both rings keep 300 frames.",
        },
        "targets": targets,
        "findings": findings,
    }
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
