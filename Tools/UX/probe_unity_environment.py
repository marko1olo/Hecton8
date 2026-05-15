#!/usr/bin/env python3
"""Probe local Unity availability for UX runtime verification."""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROJECT_VERSION_PATH = ROOT / "ProjectSettings/ProjectVersion.txt"
DEFAULT_REPORT = ROOT / "Docs/AgentLogs/UI_UnityEnvironmentProbe_UX_ENGINEER.json"
DEFAULT_ROOTS = (
    Path("C:/Program Files/Unity/Hub/Editor"),
    Path("C:/Program Files/Unity"),
    Path("C:/Program Files (x86)/Unity"),
)

UNITY_VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+[abfpx]\d+$", re.IGNORECASE)


def read_required_unity_version(path: Path = PROJECT_VERSION_PATH) -> str:
    """Read m_EditorVersion from ProjectVersion.txt."""

    if not path.exists():
        return ""

    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        stripped = line.strip()
        if stripped.startswith("m_EditorVersion:"):
            return stripped.split(":", 1)[1].strip()
    return ""


def infer_unity_version_from_path(path: Path) -> str:
    """Infer a Unity editor version from a candidate executable path."""

    for part in reversed(path.parts):
        if UNITY_VERSION_PATTERN.match(part):
            return part
    return ""


def find_unity_candidates(
    default_roots: tuple[Path, ...] = DEFAULT_ROOTS,
    explicit_paths: tuple[Path, ...] = (),
) -> list[str]:
    """Find Unity executable candidates without launching Unity."""

    candidates: list[Path] = []
    for explicit_path in explicit_paths:
        if explicit_path.exists():
            candidates.append(explicit_path)

    unity_env = os.environ.get("UNITY_EXE", "").strip()
    if unity_env:
        path = Path(unity_env)
        if path.exists():
            candidates.append(path)

    for command_name in ("Unity.exe", "Unity"):
        resolved = shutil.which(command_name)
        if resolved:
            candidates.append(Path(resolved))

    for root in default_roots:
        if not root.exists():
            continue
        for executable in root.rglob("Unity.exe"):
            candidates.append(executable)

    unique: list[str] = []
    seen: set[str] = set()
    for candidate in candidates:
        resolved = str(candidate.resolve())
        if resolved not in seen:
            seen.add(resolved)
            unique.append(resolved)
    return unique


def build_candidate_details(candidates: list[str], required_version: str) -> list[dict[str, object]]:
    """Build per-candidate version match records."""

    details: list[dict[str, object]] = []
    for candidate in candidates:
        inferred_version = infer_unity_version_from_path(Path(candidate))
        details.append(
            {
                "path": candidate,
                "inferredVersion": inferred_version,
                "matchesRequiredVersion": bool(required_version and inferred_version == required_version),
            }
        )
    return details


def resolve_probe_status(candidates: list[str], candidate_details: list[dict[str, object]], required_version: str) -> str:
    """Resolve the environment probe status without launching Unity."""

    if not candidates:
        return "UNITY_NOT_FOUND"
    if required_version and any(detail["matchesRequiredVersion"] for detail in candidate_details):
        return "UNITY_REQUIRED_VERSION_FOUND"
    if required_version and any(detail["inferredVersion"] for detail in candidate_details):
        return "UNITY_VERSION_MISMATCH"
    return "UNITY_AVAILABLE_VERSION_UNKNOWN"


def build_probe_report(explicit_unity_paths: tuple[Path, ...] = ()) -> dict[str, object]:
    required_version = read_required_unity_version()
    candidates = find_unity_candidates(explicit_paths=explicit_unity_paths)
    candidate_details = build_candidate_details(candidates, required_version)
    matching_candidates = [detail["path"] for detail in candidate_details if detail["matchesRequiredVersion"]]
    return {
        "schema": "hecton8.hardware_adaptive_ui_scaler.unity_environment_probe.v1",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": resolve_probe_status(candidates, candidate_details, required_version),
        "requiredUnityVersion": required_version,
        "unityCandidates": candidates,
        "candidateDetails": candidate_details,
        "matchingCandidates": matching_candidates,
        "defaultRoots": [str(root) for root in DEFAULT_ROOTS],
        "runtimeVerificationStatus": "PENDING_UNITY_VERIFICATION",
        "note": "Probe does not launch Unity and does not replace import, GCMonitor, Frame Debugger, or capture evidence.",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-path", action="append", default=(), help="Explicit Unity executable path candidate.")
    parser.add_argument("--write-report", action="store_true", help="Write Unity environment probe JSON.")
    parser.add_argument("--report", default=str(DEFAULT_REPORT), help="Output report path.")
    args = parser.parse_args()

    explicit_paths = tuple(Path(path) for path in args.unity_path)
    report = build_probe_report(explicit_paths)
    if args.write_report:
        report_path = Path(args.report)
        if not report_path.is_absolute():
            report_path = ROOT / report_path
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print(f"Unity environment probe {report['status']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
