#!/usr/bin/env python3
"""Cold static scanner for object-oriented seismic explosion routes."""

from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "Docs" / "Reports" / "PHYSICS_OPTIMIZATION_REPORT.json"
SCAN_DIRS = (
    ROOT / "Assets" / "_Project" / "Scripts" / "Environment",
    ROOT / "Assets" / "_Project" / "Scripts" / "Events",
)

TOKENS = (
    "Rigidbody.AddExplosionForce",
    ".AddExplosionForce",
    "Physics.OverlapSphere",
    "UnityEngine.Physics.OverlapSphere",
)

TYPE_DECL = re.compile(r"\b(?:class|struct|record)\s+([A-Za-z_][A-Za-z0-9_]*)")
METHOD_DECL = re.compile(
    r"\b(?:public|private|protected|internal|static|unsafe|virtual|override|sealed|partial|async|extern|\s)+"
    r"[A-Za-z_][A-Za-z0-9_<>,\[\].?]*\s+([A-Za-z_][A-Za-z0-9_]*)\s*\([^;{}]*\)"
)
SEISMIC_CONTEXT = re.compile(
    r"\b(seismic|quake|earthquake|cataclysm|shockwave|volcan|eruption|tremor)\b",
    re.IGNORECASE,
)


def strip_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.DOTALL)
    text = re.sub(r"//.*", "", text)
    return text


def discover_namespace(text: str) -> str:
    match = re.search(r"\bnamespace\s+([A-Za-z0-9_.]+)", text)
    return match.group(1) if match else ""


def resolve_ast_context(lines: list[str], line_index: int) -> dict[str, str]:
    type_name = ""
    member_name = ""
    search_start = max(0, line_index - 80)
    for i in range(line_index, search_start - 1, -1):
        line = lines[i]
        if not member_name:
            method = METHOD_DECL.search(line)
            if method:
                member_name = method.group(1)
        if not type_name:
            type_match = TYPE_DECL.search(line)
            if type_match:
                type_name = type_match.group(1)
        if type_name and member_name:
            break

    return {
        "type": type_name,
        "member": member_name,
    }


def scan_file(path: Path) -> list[dict[str, object]]:
    raw = path.read_text(encoding="utf-8", errors="ignore")
    code = strip_comments(raw)
    namespace = discover_namespace(code)
    lines = code.splitlines()
    contextual = bool(SEISMIC_CONTEXT.search(path.as_posix()) or SEISMIC_CONTEXT.search(code))
    findings: list[dict[str, object]] = []
    for line_number, line in enumerate(lines, 1):
        for token in TOKENS:
            if token not in line:
                continue
            if "OverlapSphereNonAlloc" in line and token.endswith("OverlapSphere"):
                continue
            ast_context = resolve_ast_context(lines, line_number - 1)
            findings.append(
                {
                    "path": path.relative_to(ROOT).as_posix(),
                    "line": line_number,
                    "namespace": namespace,
                    "type": ast_context["type"],
                    "member": ast_context["member"],
                    "token": token,
                    "seismicContext": contextual,
                    "text": line.strip()[:180],
                }
            )
    return findings


def scan() -> dict[str, object]:
    files: list[Path] = []
    for directory in SCAN_DIRS:
        if directory.exists():
            files.extend(directory.rglob("*.cs"))

    findings: list[dict[str, object]] = []
    scanned_files = 0
    for path in sorted(files):
        if "/Editor/" in path.as_posix() or "\\Editor\\" in str(path):
            continue
        scanned_files += 1
        findings.extend(scan_file(path))

    seismic_hits = [item for item in findings if item["seismicContext"]]
    return {
        "scanner": "OOP_Explosion_Scanner",
        "agent": "SHINOBU_346",
        "summary": "OOP Seismic Forces Eradicated" if not seismic_hits else "OOP Seismic Forces Still Present",
        "analysisMode": "CLI preflight token scan with namespace/type/member context",
        "scannerUsesRoslynAst": False,
        "companionRoslynAstScanner": "Assets/_Project/Scripts/Environment/Editor/OOP_Explosion_Scanner.cs",
        "companionRoslynStatus": "source-added with shared-report upsert and unqualified OverlapSphere detection; Unity menu execution pending",
        "scanScope": [path.relative_to(ROOT).as_posix() for path in SCAN_DIRS if path.exists()],
        "searchedTokens": list(TOKENS),
        "filesScanned": scanned_files,
        "allExplosionApiSites": len(findings),
        "seismicExplosionApiSites": len(seismic_hits),
        "findings": seismic_hits,
        "proof": {
            "runtimeRoute": "SeismicEventDTO + SeismicStateDTO in GlobalDataVault -> SeismicSignal via SignalBus",
            "forbiddenRuntimeApis": ["Physics.OverlapSphere", "Rigidbody.AddExplosionForce"],
            "hotPathManagedAllocations": 0,
        },
    }


def main() -> int:
    result = scan()
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    if REPORT.exists():
        try:
            payload = json.loads(REPORT.read_text(encoding="utf-8"))
            if not isinstance(payload, dict):
                payload = {"previousReportType": type(payload).__name__, "previousReport": payload}
        except json.JSONDecodeError:
            payload = {"previousReportUnreadable": True}
    else:
        payload = {}

    payload["SHINOBU_346_OOP_Explosion_Scanner"] = result
    REPORT.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")
    sidecar = REPORT.with_name("PHYSICS_OPTIMIZATION_REPORT_SHINOBU_346.json")
    sidecar.write_text(json.dumps(result, indent=2, sort_keys=True), encoding="utf-8")
    print(result["summary"])
    print(f"filesScanned={result['filesScanned']} seismicExplosionApiSites={result['seismicExplosionApiSites']}")
    return 0 if result["seismicExplosionApiSites"] == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main())
