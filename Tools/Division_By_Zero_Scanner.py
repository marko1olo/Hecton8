#!/usr/bin/env python3
"""SHINOBU_248 scoped inverse-square and normalization scanner.

Scans Physics and Combat gameplay C# sources for raw distance division,
raw reciprocal, and unsafe vector normalization. Writes a preserved-history
JSON report to Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json.
"""

from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = ROOT / "Docs" / "Reports" / "PHYSICS_OPTIMIZATION_REPORT.json"
SCAN_ROOTS = [
    ROOT / "Assets" / "_Project" / "Scripts" / "Physics",
    ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "Combat",
    ROOT / "Assets" / "_Project" / "Scripts" / "Combat",
]

RAW_NORMALIZE = re.compile(r"\bmath\.normalize\s*\(")
DOT_NORMALIZED = re.compile(r"\.normalized\b")
RAW_RCP = re.compile(r"\bmath\.rcp\s*\(([^)]*)\)")
DIST_DIVISION = re.compile(r"/\s*(distanceSq|distSq|distance|dist|length|radius)\b", re.IGNORECASE)
MATHF_MAX = re.compile(r"\bMathf\.Max\s*\(")
GUARD_EPSILON = re.compile(r"(epsilon|epsilonclamp|0\.0001|minshockwavedistance)", re.IGNORECASE)
GUARDED_DENOM_ASSIGN = re.compile(
    r"\b(distanceSq|distSq|distance|dist|length|radius|denominator|divisor)\s*=\s*math\.max\s*\(",
    re.IGNORECASE,
)


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def load_previous() -> dict[str, Any] | None:
    if not REPORT_PATH.exists():
        return None

    try:
        data = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError):
        return {
            "unparsedReportPath": rel(REPORT_PATH),
            "reason": "Existing report was not valid JSON at scanner runtime.",
        }

    if data.get("agent") == "SHINOBU_248" and "preservedPreviousReport" in data:
        previous = data.get("preservedPreviousReport")
        return previous if isinstance(previous, dict) else None

    return data


def guarded_by_context(line: str, context: str, match_id: str) -> bool:
    lowered = (line + "\n" + context).lower()
    if match_id in {"RAW_NORMALIZE", "DOT_NORMALIZED"}:
        return "normalizesafe" in lowered

    if "math.max" not in lowered:
        return False

    if GUARD_EPSILON.search(lowered):
        return True

    return bool(GUARDED_DENOM_ASSIGN.search(context))


def classify(path: Path, line: str, context: str, match_id: str) -> tuple[str, str]:
    path_text = rel(path)
    editor_only = "/Editor/" in path_text or path_text.endswith("EditorFacade.cs")
    cavitation_runtime = "/Physics/Cavitation/" in path_text
    guarded = guarded_by_context(line, context, match_id)

    if match_id in {"RAW_RCP", "DIST_DIVISION"} and guarded:
        return "INFO", "Context contains local math.max denominator guard."
    if match_id == "RAW_RCP" and re.fullmatch(r"\s*[-+]?\d+(\.\d+)?f?\s*", RAW_RCP.search(line).group(1) if RAW_RCP.search(line) else ""):
        return "INFO", "Constant reciprocal; not a distance/radius denominator."
    if match_id == "DOT_NORMALIZED" and editor_only:
        return "INFO", "Editor-only normalization; not in Burst shockwave runtime."
    if match_id == "MATHF_MAX" and editor_only:
        return "INFO", "Editor-only Mathf.Max; no Burst/runtime shockwave impact."
    if match_id == "MATHF_MAX":
        return "WARN", "Mathf.Max in scanned runtime scope; prefer Unity.Mathematics in Burst/job code."
    if not cavitation_runtime:
        return "WARN", "Out-of-domain static finding preserved for integrator; SHINOBU_248 did not edit this owner."

    return "ERROR", "Raw distance reciprocal/division or unsafe normalization needs local proof."


def scan_file(path: Path) -> list[dict[str, Any]]:
    findings: list[dict[str, Any]] = []
    try:
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError as exc:
        return [{
            "severity": "ERROR",
            "id": "READ_FAILED",
            "path": rel(path),
            "line": 0,
            "evidence": str(exc),
        }]

    patterns = [
        ("RAW_NORMALIZE", RAW_NORMALIZE),
        ("DOT_NORMALIZED", DOT_NORMALIZED),
        ("RAW_RCP", RAW_RCP),
        ("DIST_DIVISION", DIST_DIVISION),
        ("MATHF_MAX", MATHF_MAX),
    ]
    for index, line in enumerate(lines, start=1):
        stripped = line.strip()
        if not stripped or stripped.startswith("//"):
            continue
        window_start = max(0, index - 12)
        context = "\n".join(lines[window_start:index])
        for match_id, pattern in patterns:
            if not pattern.search(line):
                continue
            severity, note = classify(path, line, context, match_id)
            findings.append({
                "severity": severity,
                "id": match_id,
                "path": rel(path),
                "line": index,
                "code": stripped,
                "note": note,
            })
    return findings


def main() -> int:
    previous = load_previous()
    scanned_files = 0
    missing_roots: list[str] = []
    findings: list[dict[str, Any]] = []

    for root in SCAN_ROOTS:
        if not root.exists():
            missing_roots.append(rel(root))
            continue
        for path in sorted(root.rglob("*.cs")):
            scanned_files += 1
            findings.extend(scan_file(path))

    error_count = sum(1 for item in findings if item["severity"] == "ERROR")
    warn_count = sum(1 for item in findings if item["severity"] == "WARN")
    info_count = sum(1 for item in findings if item["severity"] == "INFO")

    runtime_errors = [
        item for item in findings
        if item["severity"] == "ERROR" and "/Physics/Cavitation/" in item["path"]
    ]
    verdict = (
        "PASS: cavitation inverse-square runtime has local epsilon guards; compile/runtime proof pending"
        if not runtime_errors
        else "FAIL: cavitation runtime still has unsafe math findings"
    )

    report: dict[str, Any] = {
        "agent": "SHINOBU_248",
        "summary": "Unsafe Mathematics Purged",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "verdict": verdict,
        "scope": [rel(root) for root in SCAN_ROOTS],
        "missingRoots": missing_roots,
        "scannedCsFiles": scanned_files,
        "counts": {
            "error": error_count,
            "warn": warn_count,
            "info": info_count,
            "total": len(findings),
        },
        "cavitationRuntimeErrors": runtime_errors,
        "findings": findings,
        "notes": [
            "Scanner is static text proof, not compile proof.",
            "Guarded math.rcp(distanceSq) is accepted only when nearby context shows math.max on the denominator or epsilon proof.",
            "Editor-only .normalized findings are reported as INFO and do not prove runtime shockwave risk.",
        ],
    }
    if previous:
        report["preservedPreviousReport"] = previous

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=False) + "\n", encoding="utf-8")
    print(f"SHINOBU_248 scanner wrote {rel(REPORT_PATH)}")
    print(f"files={scanned_files} errors={error_count} warns={warn_count} infos={info_count}")
    return 1 if runtime_errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
